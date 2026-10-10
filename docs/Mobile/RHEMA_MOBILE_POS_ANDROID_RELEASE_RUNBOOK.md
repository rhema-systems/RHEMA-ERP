# RHEMA Mobile POS Android Release Runbook

## Release boundary

This runbook governs signed UAT and production Android releases of `com.rhemasystems.fieldpos`. Two explicit hardware profiles are supported: `PortableFallback`, which excludes proprietary ZCS files and uses Android system print/PDF/share plus camera/manual/keyboard-wedge input, and `Z92S`, which packages the externally supplied audited SmartPos SDK. It does not deploy the RHEMA API or HQ web application, apply database migrations, enroll a device, or approve vendor redistribution rights.

The generated Android release task fails unless external signing is explicitly enabled and all four signing values are present. Keystore bytes, passwords, vendor JAR/JNI files, generated Android source, APKs, AABs, and local evidence remain outside Git.

## Required approvals and inputs

Before a release candidate is built, record:

1. the approved Git commit;
2. the selected hardware profile; a Z92S build additionally requires written permission to redistribute the supplied SmartPos JAR and JNI libraries;
3. the target environment and exact HTTPS API origin;
4. the approved `version` and strictly increasing Android `versionCode` in `apps/mobile/app.json`;
5. a passing `acceptance:mobile-pos` evidence file from the same commit and hardware profile with native Android compilation requested;
6. the release signing certificate owner, expiry, secure backup location, and recovery custodians;
7. the target device ring and named release approver.

The build host requires Node/npm, the repository-required .NET SDK, Java/JDK, Android SDK/build-tools, PowerShell, and Git. A Z92S build additionally requires the externally retained ZCS SDK directory. The signing keystore must be outside the repository.

On Windows, use a real short-path checkout such as `C:\rhema-mobile-release` for native builds. Do not rely on `subst` against a deeply nested checkout: React Native code generation can retain the physical dependency path while Gradle uses the mapped drive, and long physical paths can exceed CMake/Ninja limits.

## Signing secret preparation

Load signing values into process-scoped environment variables from the approved secret store. Do not paste them into source, command arguments, issue comments, logs, or the release manifest.

```powershell
$env:RHEMA_ANDROID_KEYSTORE_PASSWORD = '<from-secret-store>'
$env:RHEMA_ANDROID_KEY_ALIAS = '<approved-key-alias>'
$env:RHEMA_ANDROID_KEY_PASSWORD = '<from-secret-store>'
```

The build script sets `RHEMA_ANDROID_KEYSTORE_PATH` and `RHEMA_ANDROID_SIGNING_ENABLED` only for the build and restores their prior process values afterward. Close the build shell when the release is complete so the three operator-loaded values are discarded.

Keep at least two access-controlled, tested backups of the keystore. Losing the signing identity prevents trusted upgrades. Replacing it is a new application identity unless the distribution platform provides an approved key-rotation process.

## Acceptance gate

Run the complete gate from a clean checkout of the candidate commit. Do not use `-SkipInstall` for release evidence.

Portable fallback:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/acceptance/Invoke-MobilePosAcceptance.ps1 `
  -BuildNativeAndroid
```

Z92S:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/acceptance/Invoke-MobilePosAcceptance.ps1 `
  -ZcsSdkDirectory 'C:\secure-sdk\SmartPos_1.8.1_R231213_SDK' `
  -BuildNativeAndroid
```

The resulting `acceptance-mobile-pos.json` must report:

- `Passed: true`;
- the exact candidate commit;
- the requested `HardwareProfile`;
- `ZcsSdkPackagingRequested: false` for portable fallback or `true` for Z92S;
- `NativeAndroidBuildRequested: true`;
- all required mobile, HQ, API, test, migration, manifest, SDK hash, and native build stages passed.

## Signed APK and AAB build

Build from the same clean commit and supply the matching acceptance result.

Portable fallback:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mobile/Build-MobilePosAndroidRelease.ps1 `
  -Environment UAT `
  -ApiBaseUrl 'https://uat.example.com' `
  -PortableFallback `
  -KeystorePath 'C:\secure-signing\rhema-field-pos.jks' `
  -AcceptanceEvidencePath 'C:\release-evidence\acceptance-mobile-pos.json'
```

Z92S, only after written redistribution approval:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mobile/Build-MobilePosAndroidRelease.ps1 `
  -Environment UAT `
  -ApiBaseUrl 'https://uat.example.com' `
  -ZcsSdkDirectory 'C:\secure-sdk\SmartPos_1.8.1_R231213_SDK' `
  -KeystorePath 'C:\secure-signing\rhema-field-pos.jks' `
  -AcceptanceEvidencePath 'C:\release-evidence\acceptance-mobile-pos.json' `
  -VendorRedistributionApproved
```

For production, change `-Environment` and the API origin. The script rejects HTTP, URL paths/query strings/credentials, an in-repository keystore, a dirty Git tree, stale or incomplete acceptance evidence, missing Android tools, missing signing values, missing SDK artifacts, and reused output directories.

The script performs a clean profile-specific prebuild, builds the release APK and AAB, verifies both signatures, and writes a release manifest under `.artifacts/mobile-pos/releases/<release-id>/`. Portable fallback builds fail if proprietary ZCS artifacts appear. Z92S builds package only the hash-pinned SDK artifacts. The manifest records the exact commit, hardware profile, version, version code, environment, public API origin, signing certificate digest, acceptance evidence hash, artifact hashes/sizes, applicable SDK hashes, fallback capabilities, and stage results. It never records signing passwords.

## Release review

Two reviewers must compare:

- Git commit and approved PR;
- application ID, version, and version code;
- target environment and API origin;
- acceptance evidence hash;
- APK and AAB SHA-256 hashes;
- signing certificate SHA-256 against the approved certificate register;
- ZCS SDK hashes against the audited values;
- the absence of keystore, SDK binaries, and secrets from Git and the evidence package.

Retain the immutable APK/AAB, manifest, acceptance evidence, approval record, test record, and release notes under the organization retention policy. Store the proprietary SDK and signed artifacts only in approved restricted storage.

## UAT installation and upgrade rehearsal

Use enrolled test devices and record device serial, model, Android version, firmware, prior application version/code, release manifest hash, operator, and timestamp.

Fresh install:

```powershell
adb install '<signed-apk-path>'
```

Upgrade without deleting app data:

```powershell
adb install -r '<signed-apk-path>'
```

Before upgrade, create governed local states for cached catalogue/configuration, retained grant, pending, rejected, manual-review, and synced commands. After upgrade, verify schema migration history and every state, then complete the MPOS-0805 restart/upgrade matrix. Never use `adb uninstall`, Clear storage, or `-d` during preservation acceptance.

Run a full Z92S receipt/scanner cycle, disconnect/reconnect scenarios, device revoke, expiry, day end, Finance reconciliation, and accounting certification before production approval.

## Staged deployment

1. **Internal UAT ring:** named test devices only; complete functional, security, recovery, accounting, hardware, and battery evidence.
2. **Pilot ring:** one controlled store/shift with support and Finance monitoring; confirm server compatibility and reconcile every sale, tender, invoice, payment, till close, and deposit proposal.
3. **Limited production rings:** add stores in small groups only after the prior group reconciles cleanly and support metrics remain acceptable.
4. **General release:** release authority signs the manifest and rollout record; support receives the known-issue, recovery, escalation, and certificate/version register.

Device enrollment and effective store/till assignment remain separate server-side controls. Installing an APK does not grant access.

## Stop and recovery conditions

Pause rollout when any of these occurs:

- signing certificate mismatch or artifact hash mismatch;
- unexpected API origin/environment;
- duplicate or missing canonical invoice/payment/allocation;
- unreconciled ambiguous outbox work;
- data loss across restart/upgrade;
- systematic printer/scanner failure;
- crash, thermal, storage, battery, or latency results outside the accepted UAT limits;
- server version incompatibility or a migration/accounting gate failure.

Preserve device state, logs, correlation IDs, mutation IDs, release manifest, and Finance records. Do not clear data or manually recreate an ambiguous transaction. Reconcile the server mutation receipt and canonical Finance documents first.

## Rollback strategy

Android normally rejects a lower `versionCode`, and local schema migrations are forward-only. Therefore rollback is a controlled replacement release:

1. stop further rollout and identify affected device/store/till/session/mutation scope;
2. reconcile pending and ambiguous work against server mutation receipts and Finance documents;
3. select the last compatible source commit;
4. apply any required compatibility fix, increment `versionCode` above the faulty release, and build with the same approved signing identity;
5. repeat the full acceptance and signed-build process;
6. install with `adb install -r` or the managed distribution channel so app data is retained;
7. repeat post-upgrade state, hardware, API, and accounting checks before resuming rollout.

Do not downgrade with `adb install -d`, uninstall the app, clear application storage, restore an older local database over a newer schema, or roll back posted Finance data. Posted invoices, payments, journals, till evidence, and deposits require their canonical reversal/correction processes. If the server must also be rolled back, first prove that its API and database remain compatible with every installed mobile version and retained command schema.

## Completion evidence

MPOS-0806 can be marked complete only when a release authority has approved and retained:

- a clean, exact-commit release manifest;
- a passing native acceptance result;
- verified signed APK/AAB and certificate digest;
- UAT fresh-install and in-place-upgrade evidence on the physical Z92S;
- retained SQLite/SecureStore state checks;
- a successful last-good replacement rehearsal with a higher version code;
- server/API/database compatibility and accounting reconciliation;
- staged rollout, monitoring, support, and custody records.
