# RHEMA Mobile POS threat review

**Review date:** 2026-10-10

**Source checkpoint:** `b69159ea281589d0e196a0d95a8c04d8d1ecefcc` plus the hardening changes described below
**Target:** Expo/React Native Android application, Mobile POS API boundary, offline store, Z92S adapter, and HQ administration surface

## Status

MPOS-0802 remains **In progress**. The source review, deterministic security tests, dependency scans, and production manifest checks are complete. An authorized dynamic API assessment, signed APK analysis, rooted-device checks, TLS interception, and physical Z92S testing remain open.

## Assets and trust boundaries

| Asset | Storage or transit | Required control |
| --- | --- | --- |
| Access and refresh tokens | Android SecureStore and HTTPS request headers | Device-only protected storage, no SQLite/log persistence, rotation, logout removal |
| Signed offline grant | Android SecureStore | Tenant/user/device/store/till/session/policy binding, expiry and revocation checks |
| Offline sale commands | App-scoped SQLite outbox | No secrets, canonical hash, legal state transitions, idempotent server replay |
| Cached customer/catalogue/configuration data | App-scoped SQLite | Exact scope key, Android sandbox, disabled backup, remote revoke and retention tests |
| Finance invoices, payments, till custody and deposits | Canonical server services and SQL Server | Dynamic permissions, tenant scope, transactionality, maker/checker, audit lineage |
| Z92S printer/scanner bridge | Local Kotlin module and externally supplied SDK | Explicit packaging, fixed hashes, no tracked vendor binaries, bounded input normalization |
| Server profile | SecureStore | Absolute origin only, no embedded credentials/query/fragment, revalidation before every request |

## Implemented and verified controls

| Area | Control and evidence |
| --- | --- |
| Transport | HTTPS is required for every remote server. Cleartext HTTP is accepted only for TEST on localhost, loopback, or Android emulator host `10.0.2.2`. The profile is revalidated immediately before every request so altered stored configuration cannot receive bearer credentials. |
| Android release manifest | The tested Expo config plugin sets `android:allowBackup="false"` for every build and `android:usesCleartextTraffic="false"` for UAT/production. A production prebuild verified both generated manifest values. |
| Credentials | Access tokens, refresh tokens, installation identity, and signed grants use SecureStore with `WHEN_UNLOCKED_THIS_DEVICE_ONLY`. Logout and failed refresh clear active credentials. Tokens are not placed in URLs, application logs, SQLite payloads, or support references. |
| API response handling | Non-JSON responses are replaced with bounded messages; correlation references are retained without displaying response bodies. A single-flight refresh retries once after a 401 and clears credentials on refresh failure. |
| Offline authorization | Signed grants are matched to tenant, user, approved device, store, till, till session, policy version, walk-in customer, revocation epoch, allowed command/tender, time window, and aggregate limit at server synchronization. |
| Replay and tamper resistance | Client mutation identity plus canonical payload hash is unique per tenant/device. Exact replay returns the original result; changed payloads conflict before Finance handlers execute. |
| Local outbox | Payload serialization rejects token, password, authorization and secret-shaped keys recursively. Legal state transitions, exclusive dispatch claims, bounded retry, and interrupted-attempt recovery are tested. |
| Authorization | Mobile endpoints use dynamic `MobilePOS.*` and canonical Finance permissions rather than role names. Existing middleware validates active user and tenant membership/expiry. |
| Device governance | Enrollment, approval, assignment, heartbeat and revoke state are persisted and checked during bootstrap/grant use. Physical remote-disable timing still needs device evidence. |
| Native SDK supply chain | SmartPos binaries stay outside Git. Packaging requires explicit enablement, the external SDK directory, and exact SHA-256 matches for the JAR and both JNI libraries. |

## Findings and disposition

| ID | Severity | Finding | Disposition |
| --- | --- | --- | --- |
| MSEC-001 | High | A modified stored server profile could previously be used by the request client without another validation pass. | **Fixed.** Every request now validates the stored origin before attaching credentials; regression test proves an HTTP production origin never reaches `fetch`. |
| MSEC-002 | High | Android backup and release cleartext policy were implicit. Cached financial/customer data must not enter Android backup, and release builds must reject cleartext transport. | **Fixed.** Tested config plugin disables backup and disables cleartext for UAT/production. |
| MSEC-003 | Medium | Cached business data and the outbox are app-scoped SQLite rather than application-level encrypted. | **Open for device acceptance.** Android file-based encryption, lock policy and MDM posture must be verified on the Z92S. If management requires database-level encryption, select and certify a supported SQLCipher/native solution before release. |
| MSEC-004 | Medium | The password remains in process memory during the MFA challenge because the current login API completes MFA by resending the original login request. | **Open server contract change.** Replace this with a short-lived opaque MFA challenge identifier before production hardening is closed. Never persist or log the password. |
| MSEC-005 | Medium | Screenshot/recent-app-preview behavior has no approved policy. Blocking capture may protect customer/payment data but affects support evidence. | **Decision required.** Management/security must decide which authenticated screens require `FLAG_SECURE` before implementation. |
| MSEC-006 | Medium | Certificate pinning is not implemented. HTTPS and platform trust validation are enforced, but a compromised device trust store remains in scope. | **Decision required.** Confirm certificate ownership, rotation and emergency recovery policy before pinning. Test TLS interception on the signed candidate. |
| MSEC-007 | High | Current dependency scans report advisories that cannot be closed safely by an unreviewed major framework upgrade. `npm audit --omit=dev` reports 23 high and 16 moderate paths, many through Expo/React Native build tooling. The API scan reports ImageSharp 3.1.11 plus transitive BouncyCastle, `System.Formats.Asn1`, and `System.Security.Cryptography.Xml` advisories. | **Open coordinated remediation.** Establish reachable runtime impact, upgrade within an Expo-compatible set and supported .NET dependency graph, rerun the full gate, and document accepted build-tool-only exposure. No critical npm advisory was reported. |
| MSEC-008 | High | Root/hooking, repackaging, debugger, local database extraction, screen capture, TLS interception and physical revoke tests have not run against a signed APK on Android 14 Z92S. | **Blocked by release toolchain and physical device.** Required before MPOS-0802 can be Complete. |
| MSEC-009 | Medium | SmartPos redistribution rights were not present in the supplied archive. | **Blocked by written vendor approval.** Vendor binaries remain outside source control and distribution artifacts. |

## Dynamic test plan

Run these checks only in an authorized UAT tenant with disposable data and the signed candidate APK.

1. Intercept TEST, UAT and PRODUCTION traffic. Confirm only the explicit local TEST origin can use HTTP, UAT/production reject cleartext, TLS hostname failures stop before credentials are sent, and no token appears in URL or logs.
2. Attempt tenant, store, till, device, session, customer, sale, receipt and close-submission identifier substitution with a valid low-privilege token. Every cross-scope request must fail without disclosing whether the foreign identifier exists.
3. Remove each dynamic permission in turn and retest the related endpoint and HQ action. Role names must have no effect.
4. Revoke the user-tenant mapping, role permission, device, store assignment, offline policy and till assignment independently. Measure active-session, heartbeat, bootstrap, grant-renewal and queued-command behavior.
5. Replay an exact online/offline mutation, replay it with one changed field, alter the signed grant, change device time, exceed grant count/value, use a disallowed tender, and synchronize after expiry with occurrence inside and outside the signed window.
6. Interrupt after request send, after server commit, during response delivery, during app termination and during device reboot. Confirm one Finance result and deterministic outbox recovery.
7. Extract app files from a normal, debug and rooted device. Confirm Android backup is unavailable, SecureStore values are not readable on an unlocked backup/restore path, and data from another scope cannot be opened in the app.
8. Test malformed barcode/wedge input, oversized values, rapid repeated triggers, printer-offline states and hostile receipt/customer text. Confirm bounded input and escaped output.
9. Inspect the signed APK/AAB for debug flags, cleartext policy, backup policy, exported components, embedded secrets, unexpected ABIs, SDK hashes and signing identity.
10. Verify lock-screen, recent-app preview, screenshot and support-evidence behavior after management decides MSEC-005.

## Completion evidence required

- Remediated or formally accepted dependency findings with owner, expiry and compensating control.
- Opaque MFA challenge contract or documented security acceptance for MSEC-004.
- Recorded decisions for screenshot protection and certificate pinning.
- Signed APK/AAB static analysis and Android 14 Z92S dynamic results.
- Authenticated API authorization/IDOR/replay report against a disposable UAT tenant.
- Physical revoke, offline expiry, disconnect/reconnect, reboot, upgrade and data-retention evidence.
