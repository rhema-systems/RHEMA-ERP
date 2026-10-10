# RHEMA Mobile POS Operational Support Handoff

## Purpose and current status

This handoff defines how Operations, Finance, Security, and application support will monitor and support the RHEMA Field POS after controlled release. It uses the implemented device heartbeat, Mobile POS administration page, local Sync & exceptions screen, day-end review/report, API correlation IDs, idempotent mutation receipts, canonical Finance records, accounting reconciliation pack, and signed release manifest.

The operating model is implementation-ready but not production-approved. Named owners, approved service targets, alert delivery, physical Z92S evidence, provider support contracts, signed artifacts, and environment-specific rehearsals remain release gates.

## Support roles

| Role | First responsibilities | Must not do |
| --- | --- | --- |
| Cashier/collector | Confirm environment/store/till, report the support reference, retain the device and transaction state, use Sync & exceptions, follow approved printer/scanner recovery | Repeat an ambiguous sale, clear app data, uninstall, share credentials, manually recreate Finance records |
| Store supervisor | Confirm device/till/session ownership, contain affected checkout, compare receipt/local reference, coordinate another approved till if needed | Resolve own maker/checker exceptions, approve unsupported manual postings, move devices between stores without assignment control |
| Application support | Verify release/device/assignment/heartbeat, correlate API logs, inspect mutation/outbox state, preserve evidence, classify and escalate | Read or request passwords/tokens, edit the device SQLite database, force terminal outbox states, delete mutation receipts |
| Finance operations | Reconcile sale, invoice, payment, allocation, till close, custody entry and deposit evidence; own canonical correction/reversal decisions | Delete or rewrite posted Finance evidence, bypass canonical reversal/approval services |
| Security/identity | Review user access, tenant expiry, device enrollment/revocation, signing certificate, transport and suspected compromise | Re-enable a compromised device without investigation and approval |
| Release engineering | Build from the approved clean commit, verify signatures/hashes, execute staged rollout/upgrade/replacement procedures | Reuse a version code, distribute unsigned/debug builds, commit keystores or proprietary SDK files |
| Vendor/provider support | Support the contracted Z92S firmware/SDK or payment provider boundary with sanitized evidence | Receive RHEMA credentials, customer/payment payloads, or unrestricted production data |

Named primary/backup owners, working hours, after-hours contact method, escalation channel, and authority to stop rollout must be completed in the environment-specific go-live record.

## Operator-visible evidence

### Account & access

The mobile Account & access screen shows the current tenant, device status, assigned store and till, printer/scanner adapters, environment, application version, API origin, and device ID. **Share support details** creates a JSON bundle through the Android share sheet.

The bundle contains:

- a generated support reference and UTC timestamp;
- environment, public API origin, and application version;
- device identity/status/model/OS, heartbeat metadata, adapter keys, and revocation epoch;
- store/till/current-session identifiers;
- signed offline grant ID, policy ID, and issue/expiry times;
- up to 500 visible unresolved queue records summarized by pending, rejected, conflict, and manual-review state, with the query limit stated in the bundle.

It deliberately excludes credentials, JWT/refresh/offline-grant tokens, user/email data, customer/default-customer data, sale or line payloads, tender/payment details, bank/liquidity identifiers, and queued message bodies. The operator chooses the recipient through the native share sheet. Treat the bundle as internal operational data and retain it only with the incident record.

### Sync & exceptions

The screen shows each local reference, command type, attempt count, state, safe error code/detail, and next retry time. Server correlation references are shown when available. Rejected, conflict, and manual-review work remains visible and is not automatically reposted.

### HQ administration and Finance

- Administration > Mobile POS shows enrolled device status, store/till assignment, capabilities, version, and last-seen time.
- The day-end queue and historical report show close, pending-sync, variance, review, and deposit status.
- Server logs correlate HTTP/API failures by correlation ID.
- Mutation receipts establish whether a client mutation completed, was explicitly rejected, or conflicts with different work.
- Canonical invoice, CustomerPayment, PaymentAllocation, journal/posting, till-session, custody, and bank-deposit records remain the accounting source of truth.
- `scripts/acceptance/Invoke-MobilePosAccountingReconciliation.ps1` supplies the guarded read-only accounting certification for an authorized database/tenant/time window.

## Daily operating checks

At shift start:

1. confirm the app environment and API origin;
2. confirm the enrolled device is Active and assigned to the correct store/till;
3. confirm app/OS/firmware and printer/scanner adapters match the approved device register;
4. open the canonical till session and verify the default walk-in customer/payment methods;
5. confirm the device can reach the server, refresh reference data, print, and scan;
6. record any pre-existing pending/review queue item before taking new transactions.

During the shift:

- watch repeated correlation IDs, authorization failures, heartbeat age, sync backlog, manual-review work, printer/scanner errors, thermal/storage/battery warnings, and tender/provider failures;
- never repeat an ambiguous transaction with a new mutation identity;
- move service to another approved till only through the store/till/device assignment process.

At shift end:

1. synchronize eligible pending work;
2. review rejected/conflict/manual-review items and preserve references;
3. submit the cash count and evidence;
4. ensure unresolved sync work follows the independent HQ exception path;
5. complete Finance till approval/return, deposit proposal, and reconciliation;
6. retain the shift evidence under the approved retention schedule.

## Incident priority guide

Final response and recovery targets require management approval. Until then, these priorities define handling order rather than contractual SLA values.

| Priority | Examples | Immediate action |
| --- | --- | --- |
| P1 critical | Suspected duplicate/missing Finance document, wrong tenant/environment, signing compromise, data loss, broad inability to sell/collect, security breach | Stop affected rollout/use, preserve state, notify incident commander, Finance and Security, correlate every mutation, start formal incident record |
| P2 high | Store unable to transact, growing ambiguous queue, day end blocked, repeated API/server failure, all Z92S print/scan unavailable without acceptable fallback | Contain store/till scope, preserve support bundle/correlation IDs, establish approved workaround, escalate application/Finance/vendor owners |
| P3 medium | One device failure with another approved device available, isolated rejected command, intermittent hardware fault, degraded performance | Record evidence, use approved replacement/fallback, schedule investigation before next shift where possible |
| P4 low | Cosmetic issue, documentation question, non-blocking usability request | Record and route through normal product/support backlog |

## Triage sequence

1. **Identify:** environment, app version/code, release ID/hash, device, store, till, session, local reference/mutation ID, UTC time, and user-visible message/correlation ID.
2. **Contain:** stop retries only when instructed; keep the app installed and retain local state; revoke the device through the governed admin flow if compromise is suspected.
3. **Check scope:** one command, device, till, store, tenant, API instance, provider, or all deployments.
4. **Establish server fact:** query the mutation receipt and canonical source record before deciding whether work is pending, completed, rejected, or conflicting.
5. **Establish accounting fact:** trace invoice, payment/allocation, journal/posting event, till/custody, and deposit evidence. Use read-only reconciliation for broader windows.
6. **Recover through governed actions:** allow bounded retry for retryable pending work; route explicit rejection/conflict/manual-review and day-end sync exceptions through authorized workflows; use canonical reversal/correction for posted Finance work.
7. **Verify:** confirm the operator screen, API result, SQL/accounting state, audit trail, and affected shift report agree.
8. **Close:** record cause, exact fix/release, data/accounting disposition, evidence, recurrence control, and stakeholder approval.

## Common recovery playbooks

### Lost response or timeout during sale completion

- Do not start another sale for the same customer/cart/tender.
- Capture the local reference, support bundle, UTC time, and correlation ID.
- Reconnect and synchronize the original queued mutation.
- Confirm the mutation receipt returns the same canonical sale/invoice/payment result.
- Escalate if it enters Conflict or ManualReview or if Finance counts differ.

### Explicit rejection or conflict

- Preserve the error code/detail and original mutation identity.
- Do not edit the SQLite row or automatically requeue it.
- Resolve the underlying policy, assignment, reference, limit, session, or idempotency conflict through the authorized application/HQ process.
- If a new business transaction is required, document why it is distinct from the retained rejected work.

### Revoked, suspended, expired, or reassigned device

- Verify the server device status, assignment effective dates, user tenant access, and offline grant expiry/revocation epoch.
- Do not bypass enrollment or copy secure storage between devices.
- Preserve and reconcile queued work before retirement/reassignment. Security approval is required after suspected compromise.

### Printer or scanner failure

- Record device/firmware/app/SDK hashes and adapter result/status.
- Retry only through the implemented adapter result flow after correcting paper/power/device conditions.
- Use Android system print/PDF/share or manual/camera/keyboard-wedge scanning where approved and available.
- Do not claim a receipt printed when only a PDF was generated; retain the canonical receipt and reprint audit.

### Day end blocked by pending work

- Compare retained pending mutation IDs/digest with mutation receipts.
- Complete synchronization where the original authorization remains valid.
- Use independent HQ sync-exception resolution for unresolved governed work.
- The cashier cannot resolve or approve their own exception, and finalization cannot silently ignore ambiguous work.

### Release regression

- Stop rollout and follow `RHEMA_MOBILE_POS_ANDROID_RELEASE_RUNBOOK.md`.
- Preserve local state and reconcile ambiguous work before replacement.
- Build the last compatible source with the same signing identity and a higher version code; perform an in-place upgrade.
- Never uninstall/clear data, force a lower version code, or roll back posted Finance records.

## Monitoring and review schedule

The environment owner must implement and retain evidence for:

- API liveness/readiness, error rate, latency, and correlation-aware logs;
- active/suspended/revoked devices, app-version drift, capability drift, and heartbeat age;
- queue backlog/age by operator report until a safe centralized queue aggregate is implemented;
- mutation conflicts/rejections and duplicate-identity attempts;
- incomplete tender/payment links and accounting reconciliation findings;
- till sessions pending review, unresolved sync exceptions, variance, and delayed close/deposit states;
- crash/ANR, storage, thermal, battery, and device fleet health from the approved device-management tooling;
- signing certificate expiry/custody and distributed release hashes;
- Z92S/vendor and payment-provider incidents against their contracted support terms.

Review cadence:

- per shift: operator queue and till/day-end evidence;
- daily during pilot: device/heartbeat/version, exceptions, Finance reconciliation, support incidents;
- weekly after stabilization: trends, unresolved incidents, capacity/performance, access/device review;
- per release: manifest/signing, install/upgrade/recovery, compatibility, threat and dependency review;
- periodically per approved policy: backup/restore, signing recovery, device-revoke, network-loss, rollback, and accounting reconciliation drills.

## Go-live handoff checklist

- [ ] Named primary/backup owners and escalation routes approved.
- [ ] SLA, RPO/RTO, monitoring thresholds, alert recipients, and support hours approved.
- [ ] Production API/database/integration isolation confirmed.
- [ ] Mobile POS migrations applied and verified in the authorized environment.
- [ ] Roles receive only the dynamic Mobile POS and canonical Finance permissions required by duty.
- [ ] Stores, default walk-in customers, dimensions, tills, tender mappings, policies, and assignments approved.
- [ ] Signing identity, certificate register, secure backups, build credentials, and version register complete.
- [ ] Written ZCS redistribution approval and provider sandbox/production contracts complete.
- [ ] Signed APK/AAB manifest and physical Z92S certification retained.
- [ ] MPOS-0802 security checks, MPOS-0803 accounting certification, MPOS-0804 provider certification, and MPOS-0805 physical performance/recovery/battery evidence accepted.
- [ ] Fresh install, in-place upgrade, revoke, network-loss, restart, day-end, reconciliation, hardware, and last-good replacement rehearsals passed.
- [ ] Cashier, supervisor, Finance, support, Security, release, and vendor training completed.
- [ ] Pilot exit criteria and authority to pause/rollback are documented.

MPOS-0807 becomes complete only after the named owners fill this checklist with environment-specific evidence and sign the operational acceptance record.
