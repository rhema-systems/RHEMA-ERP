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
   traveller's privacy and groups (committed `895996b6f`).
5. **Lane 2** (the approval ladder and the approver's door, D-7) — **COMPLETE 2026-10-02**, in two
   slices: **2a** the ladder, the door, the queue and the retrofit migration (committed `4e4d85b2f`; the
   retrofit `20261002042909_TravelClosureApprovalLadder` is applied to UAT, restore point
   `ErpSystemDB_UAT_before_travell2.bak`); **2b** the screens, the last stage read from the route, and
   the workflow designer fix (cross-module defect #34) — built and proven (`run-final-approvals.mjs`
   123/123 twice, lifecycle 255/255 twice, truth 117/117 twice), committed `519418f00`.
   ⚠ Stage 1 is addressed **by name**, not by role — read lane 2's *As built* before touching the route.
6. **Lane 3** (the money chain, B9 included) — **COMPLETE 2026-10-02**, in three slices: **3a** advances,
   numbers and the lane's migration (committed `e2785ce7b`; `20261002132850_TravelClosureMoneyChain` applied
   to UAT, restore point `ErpSystemDB_UAT_before_travell3.bak`); **3b** the claim chain (committed
   `f49e5eb9c`); **3c** the budget and the payment void, with D-17's posted-path proof on a scratch copy of
   UAT (committed `b08bd498d` — money 286/286 twice, the posting proof 70/70 twice). Decisions D-14…D-17.
   ⚠ The posting proof found that **no HR posting can land on a database seeded with Finance's v2 books**
   (cross-module defect #35): it passed only once the scratch copy's primary book was renamed.
7. **Lane 4** (policy and authority) — **IN PROGRESS.** Source-checked against `b08bd498d` on 2026-10-02
   (lane 4's *Source check*): every finding holds, six more found; D-18…D-20 taken. Three slices: **4a** the
   policy itself (committed `19f20f2f2`), **4b** bookings under the policy (built and proven — policy 119/119 twice — and staged; D-1's
   booking half, D-8, C4, C5), **4c** authority (D-3, D-19). No migration — batch 1 carries every column.
8. **Then** lanes **5 → 6 → 7 → 8 → 9 → 10** in that order (§ 2). Source-check each lane against this
   document before building it — line numbers are as of HEAD `bad482a8d`.

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
| **D-14** | Lane 3 needs columns batch 1 did not add (asked at lane 3's source check, 2026-10-02) | **A small lane-3 migration** (`TravelClosureMoneyChain`, guarded SQL, proven on a scratch copy of UAT first): `StaffTravelAdvances.CancelledAt`, `CancelledById`, `CancellationReason`; `StaffTravelExpenseClaims.ReviewNotes`. Its data part, applied with slice 3a's code: `UnsettledAmount` 0 on advances not yet disbursed (moved here from batch 1, § 5), and a settlement deadline — the trip's end plus its approved policy's claim window, or 30 days — on advances with cash out and none. Rejected: reusing the rejection columns for a cancellation and keeping review reasons as trip comments. |
| **D-15** | A claim is valued in the base currency; how much of it does an advance in another currency cover? (B11) | **Finance's rate on the day the claim is paid.** The deduction is worked in the advance's own currency; whatever a rate movement leaves on the advance is refunded or written off. Rejected: the rate on the day the advance went out, which pays the traveller a windfall or a shortfall whenever the cedi moves between disbursement and spending. |
| **D-16** | Three refinements of lane 3's own checklist | **All three:** the payer may not be anyone who reviewed a line of the claim, as well as the claim's reviewer; advances only on Approved or InProgress trips (money after the trip is a claim); a budget only once the trip is approved, its total defaulting to the approved budget. |
| **D-17** | How is the posted path proven — a payment journal reversed by a void and posted again? UAT has no travel posting rule, so every travel row in the posting register is Unposted (lane 3's source check) | **On a scratch copy of UAT (2026-10-02).** UAT here is the developer's local database, so no Finance owner can open its periods or add a travel account. The posted path is proven by one run of the API against a COPY_ONLY restore of UAT on which Finance's authority is prepared (`dev-harness/hr-finance/prep-uat-finance-authority.sql`), a travel expense account added and travel's rules switched on; the copy is dropped afterwards. UAT keeps no travel rule and an empty ledger, and its suites prove the Unposted path. **Done 2026-10-02**: `run-final-posting.mjs` 70/70 twice; its first run found cross-module defect #35 — on a database seeded with Finance's v2 books no HR posting lands (lane 3, *D-17*). |
| **D-18** | C5's second half — a Critical trip needs an acknowledged risk assessment — cannot be met before lane 7: the traveller's acknowledgement sits on the desk's Write policy and the portal door is lane 7's (lane 4's source check) | **Moved to lane 7**, beside the portal's acknowledgement door. Lane 4 keeps the Prohibited refusal. |
| **D-19** | With D-3, the officer who set a trip's budget can approve it (slice 3c refuses only the traveller) | **The officer who set or last changed the budget does not approve it** — the policy's author ≠ approver, applied to the budget. Lane 4, slice 4c. |
| **D-20** | With D-3, any booking can be deleted, so a breach could be erased from D-8's register | **Pulled forward from lane 5 in part:** a flight or hotel booking that carries an exception (pending, authorised or refused) is not deleted — it is cancelled. The full "delete only while Pending" rule stays lane 5's. Lane 4, slice 4b. |

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
| **1** | Request lifecycle | batch 1 | `run-final-lifecycle.mjs` | ✅ complete 2026-10-02 — 1a `e1d050da2`, 1b `8fadfd31e`, 1c `895996b6f` |
| **2** | The approval ladder and the approver's door | data-only retrofit `20261002042909_TravelClosureApprovalLadder` | `run-final-approvals.mjs` | ✅ complete 2026-10-02 — 2a `4e4d85b2f` (retrofit applied to UAT); 2b `519418f00` (approvals 123/123 twice, lifecycle 255/255 twice, truth 117/117 twice) |
| **3** | The money chain | batch 1 + `TravelClosureMoneyChain` (D-14, applied to UAT) | `run-final-money.mjs`, `run-final-posting.mjs` (D-17, a scratch copy only) | ✅ complete 2026-10-02 — 3a `e2785ce7b`, 3b `f49e5eb9c`, 3c `b08bd498d` (money 286/286 twice, posting proof 70/70 twice on a scratch copy, lifecycle 256/256, truth 116/116, approvals 123/123 twice) |
| **4** | Policy and authority | batch 1 (no lane migration) | `run-final-policy.mjs` | ◐ source-checked 2026-10-02; D-18…D-20; 4a committed `19f20f2f2`; **4b built and staged** (policy 119/119 twice, money 286, lifecycle 256, truth 117, approvals 123 twice each); next 4c authority |
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

- [x] **Seeder:** STAFF_TRAVEL_REQUEST leaves the one-step list and is seeded through
  `EnsureSequentialWorkflowDefinitionSeededAsync` as leave is — ~~stage 1 Manager + TenantAdmin~~
  **stage 1 addressed by name, falling back to HR** (see *As built*), stage 2 HR + TenantAdmin —
  deactivating the superseded one-step row so no tenant holds two active definitions.
  `PreventInitiatorApproval` stays false; the traveller check is the control. *(2a)*
- [x] **Service:** before the engine, approve, reject and return refuse a stage-1 decision by anyone
  who is not the traveller's line authority — leave's `IsLineAuthorityAsync` extracted into a shared HR
  helper (`HrLineAuthority`), not the leave service injected — unless the traveller has no line
  authority with a login, when HR decides stage 1 and the record says why. *(2a)*
- [x] **Controller:** approve, reject and return move from `TravelWritePolicy` to `InternalOnly` with
  the service's checks (leave's shape, `LeavesController.cs:847-858`); a read door — Read OR line
  authority OR the person it waits for (`LeavesController.cs:138-154`) — on the request, its comments
  and its attachments; `GET requests/my-approvals` asks the engine (`LeavesController.cs:759`). *(2a)*
- [x] **Frontend:** a `/hr/travel/approvals` queue, added to `sidebar-hr-gates.test.ts`'s KEEP_OPEN list
  with its reason; the detail page renders for an approver with no travel permission (desk-only tabs
  hidden), so `/me/inbox`'s link opens; the approve dialog takes an approved budget, prefilled with
  the estimate. *(slice 2b)*

Suite `run-final-approvals.mjs`: the traveller's manager approves stage 1; an unrelated manager is
refused; HR approves stage 2; a traveller with no manager goes to HR; the inbox link opens.

**As built — slice 2a (2026-10-02).**

- *Stage 1 is addressed BY NAME — a deviation from this plan's "Manager + TenantAdmin", taken while
  building.* A role-based stage 1 sends the task, the *Approval Required* notification (in-app and
  email) and the inbox row to **every** holder of the Manager role — leave's stage does exactly that
  today — so every manager in the organisation would be asked about every trip and refused on click;
  and it shuts out a supervisor without the Manager role (on UAT, `she.manager`, `records.officer`,
  `auditor` and three others have reports and no Manager role), while the HR fallback D-7 asks for
  needs HR on the stage. So the stage carries two **Dynamic** approver rules reading
  `lineApproverUserId` and `lineApproverUserId2` — the logins of the traveller's two nearest line
  authorities who can sign in (supervisor first, then the head of their unit and of each unit above,
  never the traveller) — which `SimpleWorkflowService` now puts in a travel request's engine context;
  and its **required role is HR**, which the engine falls back to when no rule resolves (no line
  authority with a login). This is per-record assignment, not conditional routing: cross-module defect
  #3 is about transitions and is untouched. TenantAdmin is off stage 1 (the service would refuse them
  there anyway) and stays on stage 2 as the backstop. The rule is shared, static, over the unit of
  work (`HrLineAuthority`, Core) so the engine's context and the service name the same people; leave's
  own `IsLineAuthorityAsync` is unchanged — its owner may point it at the helper.
- *The service* (`StaffTravelRequestService`, the "approval ladder" block) works out the caller's
  standing from the engine's current step: at **Line manager approval** the engine must accept the
  caller and the caller must be one of the traveller's line authorities; when none of the two the stage
  was sent to can decide it, the travel desk (`HR.Travel.Write`, evaluated by the API's policy pipeline
  and passed in — never read from a body) decides, and the request gains an **internal note** in the
  decider's name saying why ("… at the line manager's stage by the travel desk. {who the line was}");
  a desk officer with no employee link is refused before the engine is asked. At any other step the
  engine decides alone. Nobody decides their own trip, on either stage. A request out for approval on
  the retired one-step route has no line-manager stage and is decided as that route said (UAT's demo
  TR-2026-00002 and -00003). Renaming the stage in the designer turns the service's check off; the
  engine's routing still applies (`StaffTravelApprovalLadder` documents the contract).
- *The approved budget (O-9, T-10)* is HR's, at the last stage: sent at the line manager's stage it is
  refused (422), not ignored; it must be more than zero and within the policy's single-trip limit
  (the 422 names the policy and the limit, in the policy's currency at Finance's rate); with none sent
  the estimate is approved as before. `ApprovedById` is the final approver. The approve DTO lost its
  vestigial `ApprovedById` and `ApprovedAt`.
- *The door.* `GET {id}`, its comments, attachments and attachment download, approve, reject and
  return moved to **`StaffTravelApprovalsController`** — same route, `InternalOnly` — because the desk
  controller's class-level `HR.Travel.Read` is ANDed with any action policy. The door is Read, OR the
  traveller's line authority (at any status — a manager reads their people's trips), OR the person the
  request waits for now. Approvers see internal notes: they are staff deciding the trip. The traveller
  gets no door to their own trip — the desk view carries internal notes and the portal is theirs (A6).
  New: `GET my-approvals` (the engine, then the line rule, request by request — exactly what the verbs
  accept) and `GET {id}/viewer-actions` (may I decide, at which stage, as whom, who it waits for, why
  not) for the screens. The approve answer now says whether the trip is approved or gone on to the next
  approver.
- *Seeder and existing databases.* A new tenant gets the two-stage route from
  `EnsureStaffTravelWorkflowSeededAsync` (also run by `SeedHrWorkflowDefinitionsAsync`); the shared
  stage seed record gained optional Dynamic keys and a fallback role. A database seeded before lane 2 is
  moved by the data-only migration **`20261002042909_TravelClosureApprovalLadder`**: per tenant, the
  seeded one-step route gets version 2 (same key and name, superseding it, as the designer's own
  new-version-and-publish leaves it), both stages keep the old step's approval settings, the one-step
  version is retired, requests already out for approval keep their route; two active seeded routes
  in one tenant refuse the whole migration; a tenant-authored route is left alone; Down changes
  nothing. Proven on a COPY_ONLY restore of UAT first — **27/27** (shape, rules, transitions, settings
  kept, Up twice with no change, the refusal with nothing changed, a tenant's own route untouched) —
  then **applied to UAT 2026-10-02** on the user's go, after a restore point
  (`ErpSystemDB_UAT_before_travell2.bak`), and re-verified there.
- *The demo pack* (`080-travel.mjs`) approves stage by stage: the line manager's stage by whichever of
  `gm.ops`, `head.dev`, `md.tdc` the route named (head.dev's Kumasi trip goes to gm.ops and md.tdc,
  staff's Sebrepor trip to head.dev and gm.ops), then `hr.head` with the budget. Not run on UAT —
  UAT's four demo requests are past that step; a rebuilt database exercises it.
- *Known limits, recorded.* The engine's own delegation re-addresses a by-name approval to the
  delegate, whom the line rule then refuses: a line manager away is covered by the second line
  authority the stage was also sent to (the head above), not by delegation. The two line authorities
  are fixed when the request is submitted: a supervisor changed afterwards does not receive it — the
  traveller recalls and resubmits. The generic workflow inbox still decides through the engine alone
  (cross-module defect #15).

**Suite** `run-final-approvals.mjs` (118 assertions; fixture `buildApprovalsFixture` — its own
directorate and unit with heads and a lone unit with none, under a root with no head, so no real person
is anyone's line authority; a supervisor holding only the Employee role; a supervisor with no login; an
unrelated Manager; two HR officers; a TenantAdmin for the run's policy): §1 the route's shape on UAT;
§2 the supervisor — no Manager role, no travel permission — opens, queues and decides stage 1, is told,
and so is the head of unit, by name; the unrelated Manager can open nothing, decide nothing, is not told
and has no inbox row; the head above reads but is not asked; HR waits and the supervisor cannot set the
budget; §3 HR's stage, the zero and over-limit budgets refused, the budget and the approver recorded;
§4 no line authority → the desk decides with its note, unseen by the traveller, then a second HR
officer; §5 a supervisor without a login is passed over and the head of unit rejects; §6 the head of
unit returns for revision; §7 a head's own trip goes to the head above; §8 the line authority keeps
reading. **118/118 twice on UAT, 2026-10-02** (stamps 155645, 234900). The first run (087448) failed two
assertions that expected the approver's door to answer a traveller about their own trip — the door
refuses them by design (A6), and the suite now asserts that; one run (180324) was cut short by a
transient failure of the suite's own SQL read of `Notifications` while the platform's notification
clean-up job rewrote the table, and the harness's SQL now retries a deadlock or timeout and reports
SQL Server's own message. Both runs tore down cleanly (checked with `teardown-run.mjs`).
Regression: `run-final-lifecycle.mjs` **255/255 twice** (stamps 266125, 342391 — its approvals now
take both stages, the desk deciding the first, and its clean-up tries the second HR officer before the
TenantAdmin); `run-final-truth.mjs` **117/117 twice** (411394, 422420). The API log's only errors were
the known ones: payroll's profile FK on every employee create (defect #23), the truth suite's
deliberate duplicate policy version, one demo user's identity reconciliation, and the notification
clean-up job.

**As built — slice 2b (2026-10-02).**

- *The screens.* **Travel Approvals** (`/hr/travel/approvals`, *Human Resources → Time & Leave → Staff
  Travel → Approvals*), open to every user and on the sidebar test's keep-open list with its reason —
  the server lists only what waits for the caller; each row says the stage, whether it sets the
  budget, how the caller decides (line manager and their relation, the travel desk, an approver) and
  how long it has waited. The request page draws **Approve, Reject and Return for revision** from the
  server's *viewer actions*, not from the engine's flag (the shared workflow actions keep submit and
  recall; their approve and reject are off, since they offered the buttons to anyone the engine
  listed); a banner says the stage, what follows, and whose decision it is ("Waiting for … or …").
  **`TravelApproveDialog`** asks for the approved budget at the last stage only, prefilled with the
  estimate (T-10). An approver without travel permission sees the overview, comments, attachments and
  the workflow tab — not the itinerary, bookings, finance or compliance tabs, the comment composer, the
  upload, or Edit, Cancel, Complete and Submit; **Cancel had been drawn for anyone who could open the
  page**, and is now the desk's. The back link goes to the queue for them. `useTravelAccess` gained
  `canRead`.
- *The last stage is read from the route.* 2a treated every stage after the line manager's as the last,
  so a stage an administrator adds in the middle (Finance between the line manager and HR) would have
  been offered the budget and the figure discarded. The service now reads the route — the engine's
  current step, its definition, the next approval step by order — and only the stage with none after
  it takes the budget; an earlier stage's budget is refused naming the stage that follows. *Viewer
  actions* gained `nextStageName`, and the stage-1 refusal says what follows instead of "HR decides
  after them". With Finance added, HR — last — would still set the budget; whether TDC wants Finance
  to own that figure is recorded as a question, not decided.
- *The workflow designer keeps what it does not edit* — **cross-module defect #34, found and fixed in
  this lane** (the entry records both, for the workflow owner). The designer understood only role and
  user approvers: it showed the travel stage's approver as HR (its fallback) and, on any save, deleted
  the two named-approver rules and wrote HR as a role — every HR officer would have been asked about
  every trip at stage 1, silently. The approval-stage mapping moved into
  `frontend/src/components/workflow/WorkflowDesigner.approvers.ts` (part of the designer, in its own file so
  it is tested — `WorkflowDesigner.approvers.test.ts`): other kinds of rule are
  kept as stored, shown read-only (*Named by the record*), saved back unchanged, counted by validation;
  the required role stays their fallback. Role-and-user stages load and save exactly as before (the
  test holds a verbatim copy of the old code as the reference); the travel stage exactly as UAT's API
  returns it round-trips unchanged, twice. 8 new tests; all 47 workflow component tests pass. Not
  walked in the browser. Assigning an approval stage to a person from the record in the designer stays
  the workflow owner's.

**Suite** `run-final-approvals.mjs` gains five checks (123 in all): the stage that follows is named, read
from the route; the desk's refusal at stage 1 names it rather than assuming HR; the supervisor's budget
is refused naming the stage that sets it; HR's stage has none after it; HR's queue row is marked as the
stage that sets the budget. **123/123 twice on UAT, 2026-10-02** (stamps 157115, 210672). Regression:
lifecycle **255/255 twice** (248517, 317816), truth **117/117 twice** (387218, 396916). Frontend: the
scoped travel type-check and the designer files' type-check clean, lint clean, the travel and workflow
unit tests green. ⚠ `sidebar-hr-gates.test.ts` fails on two leaves that are not travel's —
`/hr/leave/calendar` and `/hr/company-schedule/my-schedule` — exactly as it does on the committed code
before this slice; the new entry passes. Recorded, not changed here. The API log held only the known
noise (no SMTP on UAT: 1,600 notification e-mails failed; payroll's profile FK; the truth suite's
deliberate duplicate; one demo user's identity reconciliation). The screens were not walked in a
browser.

### Lane 3 — The money chain (B1–B12, B14, D-2, D-10, O-2, O-6, O-8, O-9, T-21, T-22, T-35–T-39, T-57)

- [x] **Claim state machine.** Submit: Draft or Returned → Submitted; the request Approved, InProgress
  or Completed; at least one line; every line above `ReceiptRequiredAbove` carries a receipt
  attachment; filed within `ExpenseSubmissionDays` of `TravelEndDate` or the 422 names the date.
  Review: only from Submitted or UnderReview, only to UnderReview, Approved, PartiallyApproved,
  Rejected or Returned; Approved versus PartiallyApproved is computed from the lines; nothing approved →
  "review the lines first". Pay: only Approved or PartiallyApproved with `TotalApproved > 0`;
  `PaidById` stamped; Paid is terminal. Lines are frozen from Submitted except when Returned.
- [x] **D-2:** reviewer and payer ≠ the claim's employee; payer ≠ the reviewer; advance approver ≠ the
  employee; disburser ≠ the employee and ≠ the approver — a 403 with the sentence.
- [x] **Parents (B3):** the advance must belong to the same request and employee; the claim's and
  advance's employee is the request's traveller (server-set); a receipt is an attachment of the same
  request; claims and advances only on Approved, InProgress or Completed requests.
- [x] **O-2 (T-57):** pay refused while the traveller holds a disbursed advance on the same request
  that the claim does not name, unless the payer records a waiver reason; the filing page preselects
  the outstanding advance, and its copy says recovery happens on payment.
- [x] **Valuation (B5, B11, B12):** inline lines valued; the claim's currency server-set to base;
  `GetRateToBaseAsync` returns the dated row's rate (direction per PR #99's contract, asserted against
  `GET /api/finance/exchange-rates/current/USD`); an advance in another currency converted before it
  is deducted.
- [x] **Advances:** `0 < ApprovedAmount ≤ RequestedAmount`; the total approved on a request ≤ its
  approved budget; no new advance while the traveller has an Overdue one; reject and cancel verbs
  (`TravelAdvanceStatus.Rejected = 8`, `Cancelled = 9`); `UnsettledAmount` 0 until disbursed — and the
  data step moved here from batch 1: zero it on existing Requested and Approved advances in the same
  slice as the disbursement code that sets it; the
  outstanding and overdue reads filter status; `SettlementDeadline` defaults to `TravelEndDate +
  ExpenseSubmissionDays` (or 30 days); `Overdue` set by the sweep; `WrittenOff` by an Admin verb with a
  reason; **`RecordRefundAsync`** (amount ≤ unsettled, reference, actor ≠ traveller) settles unused
  cash handed back and posts through the same adapter as a new posting event (the Finance owner told).
  *(3a; the Finance owner's note is lane 10's)*
- [x] ⚠ Flip the posting retry guard at `HrFinancePostingAdminService.cs:655` from the negative list
  to `is not (Disbursed or PartiallySettled or FullySettled or Overdue)`, or a rejected advance could
  be re-posted; regenerate the `TravelAdvanceStatus` TS union. *(3a — WrittenOff joins the list: its
  money did go out)*
- [x] **Numbers (B9):** max-based, tenant-scoped generators for claims and advances (the shape of
  `GenerateRequestNumberAsync`), with filtered unique indexes (§ 5).
- [x] **Budget (B10, O-9, T-22):** Actual = paid claims' `NetPayable` + disbursed advances; Committed
  excludes NoShow and adds cancellation fees; the budget's lines sum to `ApprovedTotal`, which defaults
  to and may not exceed the request's approved budget; the budget's currency is the request's;
  `budgets/{id}/approve` on `TravelAdminPolicy` stamps `ApprovedById/At` (approver ≠ traveller),
  shown on the card; the Finance tab shows committed and actual against the approved budget and flags
  an overrun (whether an overrun refuses is a TDC question, § 6).
- [x] **D-10:** payroll offset leaves the pay dialog and is refused by the API for new payments.
- [x] **T-39:** an Admin *void payment* (a second person, a reason) reverses the payment and the advance
  settlement it made, and asks the posting register to reverse `TravelClaimPaid`. No code in
  `Services/HR/Finance` calls back into a claim after a Finance reversal, so a reversed posting row
  stays retryable as it is.
- [x] B7: *awaiting payment* includes PartiallyApproved. B14: user ids in `UpdatedBy`; the ignored DTO
  fields removed.
- [x] **Frontend:** a line review takes an amount and a typed reason; the advance approve dialog
  prefills and caps; the overdue-settlements queue (D-5); the posting card's wording. *(3a: the advance
  dialogs and the queue; 3b: the line review. The shared `FinancePostingCard` was left as it is — its
  wording is not travel's and already says what it does; the panel's own note names the new refund and
  write-off events)*

Suite `run-final-money.mjs`: every H above as a two-actor assertion; pay twice → 422; review a Paid
claim → 422; a claim naming another employee's advance → 404; a zero-line approval → 422; inline lines
valued; a back-dated line valued at its date's rate; a deleted claim does not collide; a reversed
`TravelClaimPaid` leaves the claim Paid and a retry re-posts the same amount.

**Source check (2026-10-02, HEAD `519418f00`).** The finance service, controller, repositories, budget
rollup, currency bridge, posting factory and register, mapper, DTOs, entities and the frontend money
screens were re-read. Lanes 0 and 1 touched these files only lightly: every finding above still holds,
at the cited lines (B4's l.809 is l.810). Ten more:

- **N1 — `Overdue` has three readers that would drop it.** Claim recovery (`SettleLinkedAdvanceAsync`),
  the separation clearance (`SeparationService.cs:2314`) and the sweep's own advance query
  (`StaffTravelReminderService.cs:301`) accept only Disbursed and PartiallySettled. Given a writer, an
  overdue advance would stop being recovered by a claim (O-2's leak again), vanish from a leaver's
  clearance and stop being chased the day it became overdue. The clearance also computes the amount
  owed itself and would ignore refunds. "Cash out" is defined once and read by all of them, in the
  slice that gives `Overdue` its writer.
- **N2 — a cancelled trip keeps its undisbursed advances.** Lane 1b's cancel refuses only cash out; a
  Requested or Approved advance stays live on the Cancelled trip and can still be approved and
  disbursed — disbursement checks nothing about the trip.
- **N3 — an advance is editable after settlement.** `UpdateAdvanceAsync` refuses only Disbursed, so a
  PartiallySettled, FullySettled, Overdue or WrittenOff advance can be re-currencied and re-dated; the
  currency is not validated on update.
- **N4 — the review's notes are accepted and dropped.** `ReviewStaffTravelExpenseClaimDto.Notes` is read
  by nothing; a rejected or returned claim carries no reason anywhere (D-14 adds the column).
- **N5 — a cancelled advance has nowhere to record who and why** (D-14).
- **N6 — the receipt rule has no screen to satisfy it.** The add-line dialog never sends
  `receiptAttachmentId`; once a policy with `ReceiptRequiredAbove` is approved, every desk claim with a
  line above it would be refused at submission. 3b adds a receipt picker.
- **N7 — the rate's direction.** UAT holds GHS→USD as `Rate 0.08`, `InverseRate 12.5` — Finance's
  documented contract, *1 base = Rate target* — not the 12.5 the bridge's remarks quote. `ConvertAsync`
  reaches 12.5 through its inverse path, at today's date. B12's dated rate repeats Finance's own lookup
  (direct quote, then inverse) on the expense date rather than reading `Rate`. (`ConvertAsync` also
  rounds the rate to the target's two places, because it converts one unit; the dated rate keeps six.)
- **N8 — no money control is permission-gated.** `TravelFinancePanel` and the claim pages do not use
  `useTravelAccess`; every button renders for any reader.
- **N9 — the advance-status union escapes its enum test.** The comment inside the
  `TravelAdvanceStatus` union defeats the test's regex, so it is not compared with C#.
- **N10 — copy that contradicts the server:** the advance "recovered at approval"
  (`travel-finance.service.ts:125`, `travel-finance.ts:192`) and pay "refuses anything not Approved"
  (`travel-finance.service.ts:117`, `travel-finance.ts:244`).

Also observed: the approve-advance dialog's prefill sits in the Dialog's own `onOpenChange`, which a
controlled `open` never calls, so it probably never fires (not run); the line review is two icons with
no amount or reason; disbursement has no confirmation; UAT's only live money rows are the Kumasi
advance (GHS 2,500, disbursed, deadline 2026-11-03 — so D-14's data part touches no live row on UAT) and
the Sebrepor claim (submitted). **The posting seam on UAT (D-17):** no travel rule or account mapping exists, `JournalEntries`
is empty, only August 2026's fiscal period is open (September onward *Future*), no
`AccountingBookPeriods` row exists, and the chart has no travel expense account — so with a rule
switched on today Finance would refuse every travel posting, and the strict adapter would refuse the
travel action with it.

**Slices.**
- **3a — advances, numbers and the migration.** D-14's migration; B9 for claims and advances;
  `0 < approved ≤ requested`, the trip's approved advances within its approved budget, no new advance
  over an overdue one, edits only while Requested (N3), approve and disburse only on Approved or
  InProgress trips (D-16); D-2 on advances; reject, cancel (a trip's cancel cancels its undisbursed
  advances, N2), write-off, refund with its posting event; `UnsettledAmount` from disbursement and the
  default deadline; `Overdue` by the sweep with N1's readers; the retry guard flipped; D-15's recovery
  arithmetic. Screens: the advance dialogs, the overdue-settlements queue, gated money controls (N8),
  the union under its test (N9).
- **3b — the claim chain.** The state machine, B3 parents, B4, B5/B11/B12 (N7), B6, B7, D-2 on claims
  (D-16's line reviewers), O-2 with the waiver, D-10, B14, the review notes (N4). Screens: line review
  with an amount and a reason, the receipt picker (N6), the filing page's advance and currency, the pay
  dialog, the copy (N10).
- **3c — the budget and void payment.** B10, O-9, T-22; T-39.

**As built — slice 3a (2026-10-02).**

- *The migration* `20261002132850_TravelClosureMoneyChain` (D-14): scaffolded by the user, rewritten as
  guarded SQL, proven **38/38** on a COPY_ONLY copy of UAT (kit: session scratchpad `m2/` — the migration's
  exact SQL rendered with csc, then Up, Up again with no row changed, Down to UAT's exact schema, Up again;
  six planted advances, one per case the data part handles; a self-test that the checking helper fails a
  wrong value). **Applied to UAT 2026-10-02** after a restore point (`ErpSystemDB_UAT_before_travell3.bak`,
  COPY_ONLY, verified with `RESTORE VERIFYONLY`); history 110 rows; verified in SQL. On UAT the data part
  touched no live row. ⚠ *The kits' `expect` helper had been vacuous* since migration batch 1: the value in
  the message was read after the test, so `$?` was always 0. Fixed (`tools/migration-kit-lane2` too);
  batch 1's and lane 2's saved results were re-checked against their expectations and all hold.
- *One definition of cash out* — `StaffTravelAdvanceRules` (Core): Disbursed, PartiallySettled or Overdue
  with something unsettled; the settlement status from the figures (an overdue advance partly settled
  stays overdue); the default deadline; the cancellation. Read by claim recovery, the separation
  clearance (which now reads the unsettled amount, so cash handed back counts), the sweep's reminder query,
  the trip's cash-out refusals and the two repository reads (N1). Requested advances no longer read as
  owed anywhere.
- *Advances.* Raised only on an Approved or InProgress trip (D-16), for the trip's traveller (the create
  DTO lost `EmployeeId`, B3), for more than nothing, with a deadline not before the trip ends, and never
  for a traveller who holds an overdue advance (O-8). Edited only while Requested (N3). Approved above zero,
  no more than asked, and within the trip's approved budget — its estimate on a trip approved before lane 2 —
  with every other approved advance, each converted at Finance's rate (O-8). **D-2:** nobody approves, pays
  out, records cash back on or writes off their own advance; the approver cannot pay it out (403 with the
  sentence). Paying out starts the debt and sets the deadline when none was given (trip end + the approved
  policy's claim window, else 30 days). **Reject** (Requested; reason), **cancel** (Requested or Approved;
  reason; the trip's cancel does it for each undisbursed advance, N2), **cash handed back** (once; at most
  what is outstanding; reference), **write off** (Admin; reason; nothing owed afterwards).
- *Overdue* is written by the reminder sweep before it collects candidates (`AdvancesMarkedOverdue` on the
  run's result); reads treat cash out past its deadline as overdue before the sweep runs (`IsOverdue`).
- *D-15* — a claim recovers a foreign advance at Finance's rate on the payment date, in the advance's own
  currency, the whole advance when the claim covers it (no rounding remainder).
- *B9* — claim and advance numbers: the highest ever issued in the tenant and year, deleted rows included,
  plus one (`CountByYearAsync` removed).
- *Posting* — two events: `TRAVEL_ADVANCE_REFUNDED` (Dr staff payments clearing / Cr staff advances
  receivable) and **`TRAVEL_ADVANCE_WRITTEN_OFF`** (Dr staff receivable write-off / Cr staff advances
  receivable) — the write-off event is an addition to this plan: without it Finance's receivable outlives an
  advance travel has written off. The register rebuilds both; the retry guard is a positive list. The
  catalogue gate test has both samples (37/37 with the build's binaries).
- *Screens.* The Finance tab's money controls render only for who may use them (N8): Approve (prefilled — the
  prefill sat in an `onOpenChange` Radix never calls — and capped), Reject, Pay out (confirmed), Cancel, Cash
  back and Write off (Admin); a requested advance waits for an approved trip; reasons and refunds show under the
  status. **Staff Travel → Advances** (`/hr/travel/advances`, `HR.Travel.Read`): overdue settlements (D-5),
  cash out, all. The advance-status union is under its enum test (N9). Scoped type-check, lint and the
  travel unit tests clean; the sidebar test fails only on the two non-travel leaves recorded in lane 2. Not
  walked in a browser.
- *A second HR persona* — at the user's request, in the seeder rather than left to lane 10:
  `TdcDemoPersonaSeeder` gains **`hr.officer`** (HR + Employee, on the Human Resource Officer post, fallback
  HR Assistant), so every seed — `seed-hr-demo` or the Developer Test Data screen's Logins tier — has the
  second officer the two-person rules need (D-2 here, D-8 in lane 4). `080-travel.mjs` pays the Kumasi
  advance out as `hr.officer`; the pack's `personas.mjs` checks the login; `UAT-DEMO-DATABASE.md` lists it.
  A database seeded before 2026-10-02 gains it when the persona seeder next runs. UAT's own Kumasi advance
  (disbursed, deadline 2026-11-03) is untouched. **UAT gained it the same day**, on the user's go:
  `seed-hr-demo` after a restore point (`ErpSystemDB_UAT_before_hrofficer.bak`) and a read-only dry run of
  every orchestrator probe and of the persona resolution (UAT holds `TDC/` employees the demo seeder did not
  create, which is why the Test Data screen refuses its Logins tier there) — 2 steps ran (both write nothing
  on UAT), 21 skipped, 0 failed; one login created, `hr.officer` → TDC/00082 Esi Vanderpuye; the other 17
  personas kept their employees; `personas.mjs` signs in as all ten it checks.

**Suite** `run-final-money.mjs` (121 assertions; fixture `buildLifecycleFixture`): §1 numbers; §2 raising;
§3 approving; §4 paying out, with the disbursement's register row; §5 reject, cancel and the trip's
cancel; §6 cash back, once, posted; §7 write-off, posted, never one's own; §8 overdue before the sweep, the
sweep's mark and reminder, no new advance, a request change paying nothing out, and a claim recovering the
overdue advance; §9 a USD advance recovered at 12.5. **121/121 twice on UAT** (stamps 124762, 160270). The
first run (988288) passed every feature check and failed the two next-number checks: the harness's rewritten
check printed SQL Server's *Null value is eliminated by an aggregate* warning ahead of its answer and parsed
the warning — fixed, the answer was 0 and 0. All three runs tore down cleanly (`teardown-run.mjs`).
Regression: lifecycle **256/256 twice** (201951, 269738 — §7 pays out with the second officer, §8 raises its
advance while the trip is approved), truth **118/118 twice** (333841, 342014 — the draft trip's advance is now
a 422 naming the rule), approvals **123/123 twice** (349180, 383684). The API log held only the known noise:
payroll's profile FK on every employee create (defect #23), the truth suite's deliberate duplicate policy
version, and notification e-mails failing for want of SMTP.

**As built — slice 3b (2026-10-02).** No migration (the review notes' column came with 3a's).

- *Filing (B3, B5, B11).* A claim is the trip's traveller's and is kept in the base currency — both set by the
  server; the create DTO lost `EmployeeId` and `CurrencyCode`, the update DTO its currency. Only on a trip that
  is approved, under way or completed. The advance it names is this trip's and this traveller's (404
  otherwise) and not rejected, cancelled or written off; a receipt is an attachment of the same trip (404);
  a per-diem rate is this organisation's. Lines sent with the claim are checked and valued like any other
  (they were stored at rate 0).
- *Submitting (D-1's claim half).* At least one expense. Under the approved policy the trip was checked
  against: every expense above its receipt threshold (converted to base from the policy's currency) carries a
  receipt — **a per diem excepted**, a refinement taken while building: it is a flat allowance with no receipt
  behind it, and the demo's Sebrepor claim has one — and a FIRST submission falls within the claim window after
  the trip ends (a returned claim was filed in time). The 422s name the expenses, the threshold, the last day
  and the policy.
- *Expenses* are fixed once the claim is submitted, except on a claim returned to the claimant; changing a
  reviewed expense sends it back to be reviewed (B6).
- *Reviewing (B1, B4, D-2, N4).* Only a submitted claim or one under review; never one's own claim or
  expense. A line is approved in whole or part — the server works out the rejected part, so the two always make
  the line (`AmountRejected` left the DTO) — and any cut needs its reason. Approving the claim needs every
  expense decided and something approved, and **records Approved or Partially approved from the lines** (it
  was set outright: a claim with no reviewed line was approved and paid its claimed total while Finance
  recognised zero). Rejecting or returning needs the reason, kept in `ReviewNotes` for the claimant.
- *Paying (B1, D-2, D-10, O-2, B7).* Approved or partly approved, once; on a trip that takes claims; the
  caller's employee link now required (`PaidById` is recorded); never the claimant, the claim's reviewer or
  anyone who reviewed one of its expenses (D-16's refinement); never by payroll offset; not in full past advance
  cash the traveller holds on the trip that the claim does not name unless the payer records why
  (`AdvanceWaiverReason`). The recovery works on the approved total only. *Awaiting payment* includes partly
  approved claims.
- *B12 — the expense date's rate.* `HrCurrencyBridge.GetRateToBaseAsync` reads the rate in force on the date
  asked, with `ConvertAsync`'s own lookup order (the direct quote, then the inverse), to six places — it had
  checked the date and then converted at today's rate. **It reaches beyond travel:** HR's Finance posting
  adapter (every foreign-currency HR posting) and staff requisition costs value at their own date too. The
  adapter test's fake stored GHS → USD as 12.5 — the transposed seed, which only worked while the bridge read
  `ConvertAsync` — and now stores Finance's contract, 0.08; its expectations are unchanged (USD 100 → GHS
  1,250). The bridge's remarks record UAT's actual row.
- *Saving.* The claim paths save by change tracking: `UpdateAsync` on a claim read with its navigations marked
  the traveller, trip, reviewer and advance modified too (the tracked-graph trap). The claim's detail read
  gained the trip, the reviewer and the payer — `RequestNumber` and `FinanceReviewedByName` had always come back
  empty.
- *Screens.* The claim page: a receipt picker (the trip's attachments; the dialog opens once they are loaded,
  so the select cannot blank a linked receipt), changing an expense, a review dialog per expense (approve all
  or part, or reject, with the reason), the review outcomes (approve-as-reviewed, reject and return with their
  reason), the reviewer's notes, who paid, the waiver box when unnamed advance cash exists, no payroll offset,
  controls for the travel desk only. The filing page: no currency to choose, the advance the traveller holds
  preselected, refused politely for a trip that takes no claims. Copy that said the advance is recovered at
  approval, or that pay refuses a partly approved claim, corrected (N10). Scoped type-check and lint clean. Not
  walked in a browser.

**Suite** `run-final-money.mjs` gains §10–§14 (202 assertions): §10 filing — the traveller and currency the
server's, another traveller's advance 404, a cancelled one 422, inline lines valued (GHS 100 + USD 10 = 225);
§11 under the run's own approved policy (receipts above GHS 200, a 10-day window) — a late first submission
refused, an empty claim refused, one unreceipted expense named (the per diem exempt), another trip's attachment
404, the trip's receipt linked, then nothing added, changed or removed after submission (the suite now uploads,
so it needs the clamd stub); §12 reviewing — draft refused, one's own refused, undecided expenses refused, a
cut over the line or without a reason refused, 200 of 300 approved with the 100 rejected computed, Partially
approved worked out, its posting row; a claim with nothing approved refused and rejected with its kept reason;
a returned claim's changed expense back to Pending, resubmitted; §13 paying — the partly approved claim in the
queue, payroll offset refused, a line reviewer and the claim reviewer refused, unnamed advance cash refused then
waived with the reason, the payer recorded, paid once, not reviewed again, both posting rows; §14 B12 — a
second GHS → USD rate planted in Finance's table for the run (stamped, removed at once and checked gone in
`finally`): a USD expense 30 days old valued at 12.5, today's at 16. **202/202 twice on UAT, 2026-10-02**
(stamps 660163, 728907; every section ran — none skipped). Regression: lifecycle **256/256 twice** (768625,
836539), truth **115/115 twice** (912862, 923384 — its claim checks moved to the money suite and a claim on its
draft trip is now a 422), approvals **123/123 twice** (931619, 966528). The posting catalogue tests 37/37 with the
build's binaries. The API log held only the known noise; every run tore down clean.

**As built — slice 3c (2026-10-02).** No migration (the void's three columns came with migration batch 1).

- *The budget (B10, O-9, T-22, D-16).* Set only once the trip is approved, under way or completed; one per
  trip. **In the trip's currency**, set by the server — the create and update DTOs lost `CurrencyCode` (T-22), and
  the update DTO the `TotalCommitted`/`TotalActual` it ignored (B14). Its total, when 0 is sent, is the trip's
  approved budget (its estimate on a trip approved before lane 2), and it may not exceed it; its five parts are all
  0 or add up to the total exactly (the dialog had called a mismatch "allowed"). A budget kept in another currency
  before lane 3 moves to the trip's on its next edit, and cannot be approved until it does.
- *Approval.* `POST budgets/{id}/approve` (`TravelAdminPolicy`, the caller's employee link): never the
  traveller's own trip (403), once (422 naming the date), and re-checked against the trip's approved budget —
  `ApprovedById`/`ApprovedAt` had no writer. **Changing an approved budget withdraws its approval**; saving it
  unchanged keeps it.
- *The rollup (B10).* `StaffTravelBudgetRollup` works in the budget's currency, each figure converted at
  Finance's rate on its own date (a booking when made, a claim when paid, an advance when paid out). *Committed*
  leaves out a no-show and keeps a cancelled or refunded flight's or hotel's cancellation fee. *Actual* =
  claims paid (their net) + advances paid out less cash handed back (Disbursed, PartiallySettled, FullySettled,
  Overdue and WrittenOff) — a claim against an advance pays only the balance, so nothing is counted twice. The
  read adds both parts of *Actual*, the trip's approved budget, and an overrun flag for each figure: **flagged,
  not refused** — whether an overrun refuses is TDC's question (§ 6), and T-20 (the parts do not bind a
  booking) stays open with it. Variance is approved less actual.
- *Voiding a payment (T-39).* `POST claims/{id}/void-payment` (`TravelAdminPolicy`, employee link, a reason of
  five characters or more): only a Paid claim; never the claimant or the payer (403 with the sentence); not on a
  closed trip; refused when the advance it recovered from has since been written off. A posted
  `TRAVEL_CLAIM_PAID` row is reversed **through the register first** (`IHrFinancePostingAdminService.ReverseAsync`,
  Finance's exact reversal, its own transaction); a row that never posted is marked **Skipped** with the reason.
  Then, in one save: the claim back to Approved or Partially approved (from its lines), its payment, payer, method,
  reference and waiver cleared, nothing deducted, the approved total payable; `PaymentVoidedAt/ById/Reason` set;
  the advance's recovery undone (the deduction in the claim's currency, or at Finance's rate on the payment date
  for a foreign advance, capped at what claims settled — cash handed back is never undone) and its status worked
  out again; an internal note on the trip naming the payment, the advance, the journal reversed and the reason.
  The approval's journal stands. Paying again posts afresh — a Reversed row as its next generation, a Skipped one
  as a new attempt. Should the save fail after the reversal, the claim reads Paid beside a reversed row, which the
  register's retry posts again; the void can be repeated.
- *Screens.* The budget dialog: no currency field (the trip's is named), the total prefilled with the trip's
  approved budget, the year from the trip, the cap and the parts' sum checked before Save, reset each time it
  opens. The budget card: who approved it and when, the trip's approved budget, committed and actual in red when
  over, actual split into claims and advances, an overrun note, **Approve the budget** for a travel administrator,
  Set/Edit only on a trip that takes a budget. The claim page: **Void payment** for a travel administrator on a
  Paid claim (a reason dialog that wants five characters — `TravelReasonDialog` gained `minLength`), and the
  void's date, officer and reason shown. Scoped type-check and lint clean. Not walked in a browser.
- *Demo pack.* `080-travel.mjs` sets budgets only on the trips it approves (Kumasi and Sebrepor), without a
  currency; Lagos and London, left submitted, take none — a database seeded before 3c keeps the ones it has.
  Neither is approved: approving a budget and voiding a payment are `HR.Travel.Admin`'s, and no demo persona
  holds it (the guide's Rule 2, where both acts and the write-off are now listed).

**Suite** `run-final-money.mjs` gains §15–§16 (286 assertions): §15 the budget — none on a draft trip; above the
trip's approved budget refused; parts adding to 900 of 1,000 refused, naming the 900; set with no total and no parts →
1,000 in the trip's currency though the payload said USD; a second refused; HR cannot approve (403), the travel
administrator can, once, recorded by name; a re-split withdraws the approval, an unchanged save keeps it; the
administrator's own trip refused; committed 370 from a 320 booking, a no-show of 200 and a 150 fare cancelled with a
fee of 50; actual 400 from an advance paid out, 300 once 100 is handed back, and 500 (claims 200 + advances 300) once a
claim of 500 recovering the 300 is paid; cut to 400, actual flagged as an overrun, variance −100. §16 the void — a
four-character reason refused, HR refused, the administrator voids: the claim Approved, payment and payer cleared,
500 payable, the void recorded by name with its reason; the advance back from Fully settled to Partially settled with
300 owed; the payment's register row Skipped and the approval's untouched; the budget's claims paid back to 0; the
internal note (not the traveller's) naming the advance, the 300 and the reason; no second void; paid again, the 300
recovered again, the row Unposted again; the payer cannot void their own payment; the claimant cannot void theirs; a
payment whose advance was later written off cannot be voided. **286/286 twice on UAT, 2026-10-02** (stamps 992779,
077210; §15 49 checks, §16 35 — none skipped). Regression: lifecycle **256/256 twice** (124715, 193277 — §11's budget
on a closed trip sends no currency), truth **116/116 twice** (255937, 265254 — §7's budget on its draft trip is now a 422
naming the rule), approvals **123/123 twice** (273481, 308759). The posting catalogue tests 37/37 with the build's
binaries. The API log held only the known noise (payroll's profile FK, defect #23; the truth suite's deliberate
duplicate policy version; e-mail with no SMTP; one identity-reconciliation job error that predates travel).

**D-17 — the posted path, on a scratch copy of UAT.** `run-final-posting.mjs` (70 assertions) against a COPY_ONLY
restore of UAT (`ErpSystemDB_TravelPostProof`; kit in the session scratchpad `l3c/d17`), on which Finance's authority
was prepared (`prep-uat-finance-authority.sql`), a travel expense account **6150** and a write-off account **6650**
were added (copies of 6000's row — UAT's 6600 is a control account), every role mapped and travel's five rules
switched on; the API started against it by `dev-harness/hr-travel/tools/start-api-scratch.ps1`, which refuses UAT,
and the suite refuses any other SQL target and stops before switching a rule on unless the API is on the copy too.
Every journal is read back from Finance: disbursement Dr 1120 400 / Cr 1010 400; cash back Dr 1010 100 / Cr 1120 100;
claim approval Dr 6150 500 / Cr 2120 500; payment Dr 2120 500 / Cr 1120 300 / Cr 1010 200; **the register's reversal
of the payment** — a reversal journal, the claim still Paid (nothing in Finance calls back into travel, T-39's note) —
and its **retry**, the same amounts as the row's second generation in a new journal; **the void**, reversing that
posted payment through the register (the reversal's reason naming the void, the trip's note naming the journal), the
claim approved again with its approval's journal standing, 300 of the advance owed again; **paid again**, posted as
the third generation for the same amounts; a write-off Dr 6650 300 / Cr 1120 300. **70/70 twice, 2026-10-02**
(stamps 677609, 702006). The copy was dropped; UAT still has no travel rule, no mapping, its own three books and no
6150 or 6650.

⚠ **The first run failed at the first posting, and that is a finding: cross-module defect #35.** HR's posting store
takes the book from Finance's V1 resolver, which answers `IFRS` from UAT's `SubledgerPostingMode`; a database seeded
on Finance's book model v2 (2026-09-21) has `BASE`, `IFRS_ADJUSTMENTS` and `USD_PARALLEL` and no `IFRS`, so Finance
refused — *"Accounting book is unavailable for this tenant"* — and the strict adapter refused the disbursement and the
claim approval with it. **No HR posting rule can be switched on on such a database** until Finance's V2 cut-over
gives producers a book that exists. The proof renamed the copy's primary book `BASE` to `IFRS`
(`l3c/d17/scratch-book.sql`) to get past it; `HR-FINANCE-POSTING-DESIGN.md` § 5.1 now says its prep table is out of
date.

### Lane 4 — Policy and authority (C1–C6, D-1, D-3, D-8, O-3, O-4, O-5, T-1, T-2, T-9, T-46, T-50, T-52)

- [ ] **D-3 in four steps:** add `AdministerTravel` to `HrStaffGrants` (`HrPermissions.cs:687`;
  precedent `AdministerRecruitment`, l.693); the seeder's grant loop is add-only and runs every
  startup, so restarting the UAT API converges existing tenants (`RoleRevocations` is not touched);
  the suite asserts the `hr` actor passes an Admin route, and `mintTravelAdminActor` is retired;
  update the remarks that describe the travel tier (`HrPermissions.cs` ~l.280, 584, 611;
  `StaffTravelMeController.cs:186`). The UI's `canAdmin` drops its `HR_ADMIN_ROLES` fallback.
- [x] **D-8:** flight and hotel bookings gain `ExceptionState` (None, Pending, Authorised, Refused),
  `ExceptionRequestedById` and `ExceptionAuthorisedById/At`. An over-cap booking is saved Pending and
  cannot be Confirmed or Ticketed until a **different** Admin holder authorises it; the
  `AdvanceBookingDays` override takes the same path; a breach register under Staff Travel lists every
  over-cap booking.
- [x] **D-1:** *(the policy half — the two fields out, the form saying what binds — done in 4a; the booking half is
  4b's)* `AdvanceBookingDays*` at flight and hotel create; `PreferredVendorMandatory` → a Supplier
  is required (picker on the four booking dialogs, tenant-validated); `RequiresCheapestFare` and
  `MaxAnnualTravelBudget` leave the DTOs and the form; the form and `PolicyRulesPanel` say exactly what
  binds, and the rules panel keeps its read-only, non-binding banner (D-29).
- [x] **C2, C3:** cabin classes validated as enum members; a policy `CurrencyCode` (the hotel cap
  compared in it, a booking in another currency converted through the bridge); `VersionNumber`
  server-assigned (max + 1 per policy name) with a filtered unique index; `EffectiveFrom ≤ EffectiveTo`;
  the level band in rank order; author ≠ approver.
- [x] **O-4:** approving a future-dated version sets the sitting version's `EffectiveTo` to the day
  before and keeps both in force for their own windows; resolution picks by date.
- [x] **O-5:** the guard resolves the unit from the traveller, not the request, and walks the unit's
  ancestry (`UnitAncestryAsync`), most specific first.
- [x] **C4:** deciding an exception → Admin, `DecidedAt` from the clock, decider ≠ requester (still no
  screen). **C5:** `RiskLevel.Prohibited` refuses submit; `Critical` needs an acknowledged risk
  assessment before departure — **moved to lane 7 (D-18)**.
- [x] **C6:** one shared line in `SelectField.onValueChange` (`fields.tsx:336-338`): `if (next === '')
  return;` before `form.setValue`. Safe: the only legitimate clear is `NONE_VALUE → ''`, and no HR
  option array declares `value: ''` (750 uses in 208 files). Scoped type-check afterwards.
- [ ] **D-19:** the officer who set or last changed a budget does not approve it. **D-20:** a flight or
  hotel booking carrying an exception is not deleted. *(D-20 done in 4b; D-19 is 4c's)*

Suite `run-final-policy.mjs`. (The plan said to re-run `run-slice12-policy-authoring.mjs` too; D-13 keeps the
slice suites off UAT, so it is **retired by name** here — `run-final-policy.mjs` re-proves what it covered.)

**Source check (2026-10-02, HEAD `b08bd498d`).** The permission map and its seeder, the role-fallback handler,
the policy service, guard, repository, DTOs and mapper, the booking service and controller, the exception flow,
the risk-assessment acknowledgement, the policy form, the access hook and the shared select were re-read. Every
finding above holds: HR's grant list still lacks `AdministerTravel` (adding it converges at once through the
role-fallback handler and durably through the startup seeder); the cabin classes are bare `[Required]` enums;
the policy entity has batch 1's `CurrencyCode` and the DTOs do not; `VersionNumber` is the payload's; no date or
band check; a policy's author may approve it; approval stands every sibling down at once, and resolution needs
`IsCurrentVersion`; scope is an exact unit match (`HrAudienceResolver.UnitAncestryAsync` exists — the unit then
its ancestors, nearest first, tenant explicit); the booking cap guard records a flag, a reason and no authoriser;
`AdvanceBookingDays*` and `PreferredVendorMandatory` have no reader, and no booking dialog sends a `VendorId`
(four booking types carry one, FK to Procurement's `Supplier`, read by HR through `api/hr/suppliers`); exceptions
are decided on Write with the body's status and date; `TravelRiskLevel.Prohibited` prohibits nothing; the select
still writes a blank. Batch 1 carries every column the lane needs — **no lane-4 migration**. On UAT: one policy
(unapproved), no booking exception, one policy exception, three suppliers.

Six more:
- **P1 — a booking's cabin class has C2's defect.** `BookingClass` is a non-nullable enum too: omitted, it is
  stored as 0 and passes any cap (0 ≤ cap). C2 covers both.
- **P2 — the request's risk level is the requester's own.** A desk risk assessment rating the destination
  Prohibited refuses nothing either; the trip's level is the higher of the two.
- **P3 — the traveller cannot acknowledge a risk assessment.** The route is the traveller's own but sits on
  `TravelWritePolicy`, and the portal door is lane 7's (D-5) — hence D-18.
- **P4 — with D-3 every booking is deletable**, so a breach could vanish from D-8's register (D-20). Itinerary and
  compliance deletes have no status guard either; lane 7 keeps them.
- **P5 — with D-3 the budget's setter can approve it** (3c refuses only the traveller) — D-19.
- **P6 — the demo pack decides its policy exception as `hr.head`, who raised it**; C4's decider ≠ requester
  refuses that, so the pack decides as `hr.officer` (slice 4b).

**Slices.**
- **4a — the policy itself.** C2 (policy and booking classes, P1); C3 (the policy's currency on the DTOs,
  defaulting to base, and the hotel cap compared in it; `VersionNumber` server-assigned; dates; the band in
  rank order; author ≠ approver); O-4; O-5 (the traveller's unit and its ancestry); D-1's policy half
  (`RequiresCheapestFare`, `MaxAnnualTravelBudget` out of the DTOs and the form; the form says what binds); C6.
- **4b — bookings under the policy.** D-1's booking half (`AdvanceBookingDays*` at flight and hotel create;
  `PreferredVendorMandatory` → a supplier on the four bookings, tenant-validated, with a picker); D-8 (the
  exception state, authorise and refuse by a different Admin holder, Confirmed/Ticketed refused until authorised,
  the breach register); D-20; C4; C5's Prohibited half (P2). The demo pack's exception decided as `hr.officer`.
- **4c — authority.** D-3 (the grant, the remarks, the UI's `canAdmin` without its role fallback, the reminders
  screen open to HR — T-52); D-19; every suite expectation that HR is refused an Admin route turned round.
  Last, because D-3 leans on 4a's and 4b's two-person rules.

**As built — slice 4a (2026-10-02).** No migration.

- *Shape (C2, C3, P1).* A policy's two cabin classes must be real classes (`Enum.IsDefined`) — omitted, they were
  stored as 0, below every class, so once approved every flight exceeded the cap; a flight's booked class too (P1:
  0 passed any cap). The end is not before the start; the unit and the two staff levels are this organisation's
  (404); the band runs from the lower rank to the higher (`Covers` reads it that way — on UAT rank 1 is
  Management). The same checks run at approval, for drafts written before lane 4, which also refuses a draft whose
  end has passed.
- *Currency (C3, T-9).* The DTOs carry batch 1's `CurrencyCode`: given, it is checked against HR's list; omitted,
  the policy keeps its own or takes the base. The hotel cap is compared in it — `StaffTravelBookingService`
  converts the booked rate through `HrCurrencyBridge` at the booking's date and the guard's 422 gives both
  ("USD 100.00 (GHS 1,250.00) exceeds … GHS 900.00").
- *Versions (T-50).* The server numbers them: the highest version of the name in the tenant, deleted drafts
  included, plus one; a renamed draft becomes the next of its new name. `VersionNumber` and `IsCurrentVersion`
  left the DTOs.
- *Author ≠ approver (C3).* Whoever created or last changed the draft (`CreatedBy`/`UpdatedBy`, the platform user)
  does not approve it — 403 with the sentence.
- *Dates (O-4).* Approval makes room by date among the versions in force for the same scope: one that started
  earlier keeps going until the day before the new one starts (`EffectiveTo` set); one starting on or after it, and
  overlapping, is stood down; one with no common day is left alone. Both stay current; the repository's date filter
  picks the version for a trip's departure. Saved by tracking — `UpdateAsync` on the siblings, read with their
  rules and approver, would have marked those modified too.
- *Scope (O-5).* `StaffTravelPolicyGuard` takes the traveller's unit from their employee record (the request's only
  for a traveller with none) and asks `IHrAudienceResolver.UnitAncestryAsync` for the chain; the repository matches
  any unit in it and orders nearest first, then banded before unbanded, then the latest start. The `applicable`
  endpoint resolves the same way.
- ⚠ *A memory grant, caught by the second suite run.* The first build's resolution query kept the old
  `Include(Rules)` and `Include(ApprovedBy)` (a whole `Employee` row) and sent the unit chain as a JSON list: SQL
  Server sized its memory grant at **~600 MB and used 16 KB** (`sys.dm_exec_query_stats`, `max_ideal_grant_kb`
  606,952), and when workspace memory was busy it queued (`RESOURCE_SEMAPHORE`, seen live with the DMVs) — four policy
  previews took 25 s in the second run, while the first ran clean (the performance closure's E-d1 pattern: a small
  read whose wide includes size the grant).
  Rebuilt: the resolution reads the policies alone, untracked, and filters the unit chain in memory (a tenant has a
  handful of policies in force on a date); the `applicable` endpoint reads the approver's name and the rule count in a
  narrow projection of its own. The new statement's grant is **0 KB**, its worst time under 1 ms.
- *D-1's policy half.* `RequiresCheapestFare` and `MaxAnnualTravelBudget` left the DTOs and the form (the columns
  stay). The form says what binds once approved (per-trip limit at submission; receipts and the claim window on
  claims) and what does not yet (booking days ahead, preferred vendors — 4b); the scope note explains ancestry and
  dated supersession; the currency is chosen on the form; the end date is validated client-side too. The policy
  page shows the currency and drops the two fields; the register's approve toast reports the state the policy is
  in (in force, or from its date).
- *C6.* `SelectField.onValueChange` ignores `''` — the only real clear is the None item. Scoped type-check and lint
  clean; not walked in a browser.
- *Demo pack.* `080-travel.mjs`'s policy sends no version, no current flag and neither retired field, and names
  GHS.

**Suite** `run-final-policy.mjs` (new; fixture `buildApprovalsFixture` — a directorate with a unit under it and a
lone unit, policies scoped to them only): §1 shape — no class 422, dates backwards 422, a band from Junior Staff down
to Management Staff 422 and in rank order accepted, a foreign unit 404, no currency → GHS, `usd` → USD, an unknown
currency 422; §2 versions — 7 in the payload → 1, then 2, version 2 deleted → the next is 3, a renamed draft is 1 of its
name; §3 the administrator's own draft refused (403, with the sentence), an HR draft the administrator changed refused,
an untouched one approved, once; §4 the directorate's policy covers a traveller in the unit under it (it matched
exactly), not one in the lone unit; the unit's own policy wins for its traveller and the directorate's staff keep
the directorate's; §5 version 2 approved to start in 60 days leaves version 1 in force until day 59 (a trip at day 40
under v1, at day 70 under v2); version 3 starting the same day replaces v2; an ended draft refused; §6 on an approved
trip at day 40 under v1 (cap GHS 900): GHS 950 refused, GHS 900 accepted, USD 100 refused naming GHS 1,250 and the cap,
USD 60 accepted; §7 a flight with no class 422, Economy accepted. On the first build 52/52 twice (194938, 237259 —
the second run's four 25 s previews are the memory grant above); **on the rebuilt one 52/52 twice (838127, 858328;
every section ran, no slow request)**. The truth suite's §7 now asserts two drafts of one name are versions 1 and 2
(the 409 they hit is gone): 116 → 117. Regression on the rebuilt binary: money **286/286 twice** (865025, 891248),
lifecycle **256/256 twice** (913425, 949815), truth **117/117 twice** (980644, 986185), approvals **123/123 twice**
(990748, 009065) — none with a slow request; on the first build the same counts twice each. The API log held only
the known noise (payroll's profile FK, defect #23; one identity-reconciliation job error that predates travel).
`run-slice12-policy-authoring.mjs` is retired by name (D-13); this suite re-proves it.

**As built — slice 4b (2026-10-02).** No migration (batch 1's `ExceptionState`, `ExceptionRequestedById`,
`ExceptionAuthorisedById/At` on flights and hotels).

- *Notice (D-1).* `AdvanceBookingDaysFlight/Hotel` are read: a flight booked fewer days before the trip's departure,
  or a hotel before its check-in, than the approved policy asks is a breach — counted from the day the booking was
  made (`CreatedAt`), so an edit does not move it. `TravelPolicyCaps` carries both and `PreferredVendorMandatory`.
- *Suppliers (D-1).* A booking's `VendorId` is this organisation's supplier (404 otherwise) and an active one when
  newly named; under a preferred-vendors policy every flight, hotel, car rental and ground-transport booking names one
  (a company vehicle excepted — no supplier provides it). The four booking dialogs carry the shared `SupplierPicker`
  (`api/hr/suppliers`).
- *The exception (D-8).* `StaffTravelPolicyGuard` now only describes a breach (`FlightClassBreach`,
  `HotelRateBreach`, `AdvanceBookingBreach`) — its `Require*WithinPolicy` granted the exception in the same request to
  a caller holding `HR.Travel.Admin`, and the controller's `CallerMayApproveExceptionsAsync` is gone. A breaching flight
  or hotel is refused unless the desk asks for an exception with the reason (the dialogs' switches now ASK); with one it
  is saved **Pending** (`ExceptionRequestedById` = the caller's employee record), and is refused Confirmed, Ticketed or
  Completed until **Authorised**. An authorised or refused exception stands while the facts it was decided on stand
  (a flight's class; a hotel's rate, currency and check-in) and goes back to Pending when they change. `POST
  flights|hotels/{id}/exception/authorise|refuse` (`TravelAdminPolicy`, employee link): never the one who asked for it,
  the booking's last writer or the traveller (403 with the sentence); a refusal's reason (≥5) is an internal note on
  the trip; `ExceptionAuthorisedById/At` record the decider either way. **Staff Travel → Policy breaches**
  (`/hr/travel/breaches`, `GET bookings/exceptions?state=`) lists flights and hotels with an exception, pending
  first, with Authorise and Refuse for an administrator; the trip's booking rows badge the state. The register and
  the exception names are narrow projections — no whole `Employee` rows (4a's memory-grant lesson; the new reads'
  grants are under 1 MB).
- *D-20.* A flight or hotel booking with an exception is not deleted — 422, cancel it.
- *C4.* Deciding a policy exception is `TravelAdminPolicy`; only Approved or Rejected; `DecidedAt` the clock's
  (`DecidedAt` left the DTO); never by whoever raised it (its `CreatedBy`).
- *C5 (Prohibited; P2).* A trip rated Prohibited — or whose latest risk assessment valid on the departure date says so
  — is refused at submission and at every approval stage. The Critical half is lane 7's (D-18).
- *Screens and copy.* The bookings panel's switches ask rather than grant, with the wording of D-8; the policy form
  says the notice and the preferred-vendor rule bind. Scoped type-check and lint clean; the sidebar gate test fails only
  on the two pre-existing leaves. Not walked in a browser.
- *Demo pack.* `080-travel.mjs` decides its policy exception as `hr.officer` — raised as `hr.head` — which works once
  HR holds Admin (4c).

**Suite** `run-final-policy.mjs` gains §8–§12 (119 assertions): on the lone unit's approved policy (30/14 days'
notice, preferred vendors) and a trip departing in 10 days — §8 a late flight refused without an exception, refused
without a reason, refused Confirmed, saved Pending and asked for by the booking officer; a hotel breaching rate and
notice refused naming both, saved Pending with an exception; §9 a flight and ground transport without a supplier
refused, a foreign supplier 404, a car rental with one accepted; §10 HR (no Admin yet) cannot authorise; the
administrator authorises (recorded by name), once; the authorised flight confirmed with its exception standing; the
administrator's own booking not authorised by them; a refusal needing five characters, recorded Refused with the
internal note; a refused flight not ticketed; a re-booked class asking again (Pending); a hotel's authorised exception
back to Pending when the rate changes; the register's pending and full views; §11 the authorised flight and the pending
hotel not deleted, a within-policy flight deleted; §12 a policy exception: another HR officer 403, "Pending" 422, the
administrator approves at the server's time (the payload said 2000-01-01), the administrator's own raised exception
403; a trip rated Prohibited not submitted, one assessed Prohibited not submitted, one assessed Prohibited after
submission not approved. **119/119 twice on UAT** (555393, 597136; every section ran — §8 13, §9 6, §10 25, §11 4,
§12 15). Regression: money **286/286 twice** (610815, 650142), lifecycle **256/256 twice** (716068, 779855), truth
**117/117 twice** (815614, 820901), approvals **123/123 twice** (825756, 843253). Two submissions took 12–15 s (one in
money's second run, one in lifecycle's first); no SQL statement of the hour took over 0.75 s, the new risk-assessment
read under 10 ms with no grant, and the machine had 2.3 GB of 24 GB free — the low-memory stall the harness memory
records, not this slice. The API log held only the known noise.

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
| **TDC questions:** reminder windows (90/14/90 days); the insurance and passport windows of O-16; whether an approved-budget overrun refuses or only warns (O-9); whether a Critical or Emergency alert blocks booking (T-45); if a Finance stage is added to the travel route, whether Finance — not the last approver (HR) — should set the approved budget (lane 2) | Recorded in `HR-OPEN-QUESTIONS-FOR-TDC.md` by lane 10 |

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
- **2026-10-02, later** — The user committed slice 1c (`895996b6f`). **Lane 2, slice 2a built**: the
  two-stage ladder with stage 1 addressed by name (a deviation from the plan's role-based stage 1,
  recorded in lane 2's *As built*), the line rule in the service with the desk's fallback note, the
  approved budget at HR's stage, the approver's door, queue and viewer actions on a new controller, the
  seeder and the data-only retrofit. One build error (the create's `CreatedAtAction` named the moved
  read) fixed; the user scaffolded `20261002042909_TravelClosureApprovalLadder` (empty, model
  unchanged), the SQL proven 27/27 on a scratch copy of UAT went in byte for byte, the build succeeded,
  and on the user's go the API was started on UAT after a restore point — the retrofit applied and
  re-verified. `run-final-approvals.mjs` 118/118 twice, lifecycle 255/255 twice, truth 117/117 twice.
  Staged. Next: slice 2b, the screens.
- **2026-10-02, later** — The user committed slice 2a (`4e4d85b2f`). Checking how the travel route would
  survive an administrator's edit found **cross-module defect #34** — the workflow designer deletes an
  approval stage's named-approver rules on save; recorded for the workflow developer, raised with them,
  and the user chose to fix the designer's side in this lane. **Slice 2b built — LANE 2 COMPLETE**: the
  approvals queue and the approver's view, travel's own decision buttons and the budget dialog, the last
  stage read from the route, and the designer fix with its round-trip tests. The build succeeded; no
  migration pending on UAT; approvals 123/123 twice, lifecycle 255/255 twice, truth 117/117 twice.
  Staged. Next: lane 3.
- **2026-10-02, later** — The user committed slice 2b (`519418f00`). **Lane 3 source-checked**: every
  finding holds; ten more found (N1–N10); split into 3a, 3b, 3c; the user took the recommendations as
  D-14, D-15 and D-16, and asked whether travel posting can be switched on in UAT (D-17, open). Slice 3a
  began with D-14's model change (three cancellation columns on the advance, the claim's review notes),
  handed to the user to scaffold.
- **2026-10-02, later** — The user scaffolded `TravelClosureMoneyChain`; rewritten as guarded SQL, proven
  38/38 on a scratch copy of UAT (the proof found the kits' vacuous `expect` helper — fixed; earlier
  migrations re-checked and hold). D-17 settled: UAT is the developer's local database, so the posted path is
  proven on a scratch copy. **Slice 3a built**: the build succeeded, the posting catalogue tests 37/37, a
  restore point taken, the API started on UAT applied the migration (verified in SQL);
  `run-final-money.mjs` 121/121 twice, lifecycle 256/256 twice, truth 118/118 twice, approvals 123/123
  twice. Staged. Next: slice 3b, the claim chain.
- **2026-10-02, later** — At the user's request the persona seeder gained `hr.officer` (a second HR desk
  officer) and `seed-hr-demo` ran on UAT after a dry run and a restore point; the user committed slice 3a
  (`e2785ce7b`). **Slice 3b built** — the claim chain: the build succeeded, the posting catalogue tests 37/37,
  no migration; `run-final-money.mjs` 202/202 twice, lifecycle 256/256 twice, truth 115/115 twice, approvals
  123/123 twice. Staged. Next: slice 3c, the budget and void payment, with D-17's posting proof on a scratch
  copy of UAT.
- **2026-10-02, later** — The user committed slice 3b (`f49e5eb9c`). **Slice 3c built — LANE 3 COMPLETE**: the
  trip budget (in the trip's currency, within its approved budget, approved by a travel administrator, *Actual*
  counting advances, an overrun flagged) and the payment void (T-39). The build succeeded, the posting catalogue
  tests 37/37, no migration; `run-final-money.mjs` 286/286 twice, lifecycle 256/256 twice, truth 116/116 twice,
  approvals 123/123 twice. **D-17 done**: `run-final-posting.mjs` 70/70 twice on a scratch copy of UAT, every
  journal read back from Finance, the copy dropped — after its first run found **cross-module defect #35** (no HR
  posting can land on a database seeded with Finance's v2 books). The demo pack's budgets follow the new rules.
  Staged. Next: lane 4, policy and authority.
- **2026-10-02, later** — The user committed slice 3c (`b08bd498d`). **Lane 4 source-checked**: every finding holds;
  six more (P1–P6); split into 4a, 4b, 4c; the user took the recommendations as D-18 (C5's Critical half to lane 7),
  D-19 (the budget's setter does not approve it) and D-20 (a booking with an exception is not deleted).
  **Slice 4a built** — the policy itself (shape, currency, server versions, author ≠ approver, dated supersession,
  unit ancestry, the two retired fields, C6). The build succeeded; no migration. The second policy run caught the
  resolution query's ~600 MB memory grant (four 25 s previews); fixed, rebuilt — grant 0 KB. On the rebuilt binary
  `run-final-policy.mjs` 52/52 twice, money 286/286, lifecycle 256/256, truth 117/117, approvals 123/123, each twice,
  no slow request. Staged. Next: slice 4b, bookings under the policy.
- **2026-10-02, later** — The user committed slice 4a (`19f20f2f2`). **Slice 4b built** — bookings under the policy: the notice and
  preferred-vendor rules read (D-1), a breaching booking saved awaiting a different administrator's authorisation and
  the Policy breaches register (D-8), no delete of a booking with an exception (D-20), policy exceptions decided by an
  administrator who did not raise them (C4), Prohibited refused (C5). The build succeeded; no migration.
  `run-final-policy.mjs` 119/119 twice, money 286/286, lifecycle 256/256, truth 117/117, approvals 123/123, each twice.
  Staged. Next: slice 4c, authority (D-3, D-19).
