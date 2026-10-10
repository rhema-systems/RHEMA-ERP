# RHEMA Mobile POS acceptance gate

## Purpose

`acceptance:mobile-pos` is the repeatable source and packaging gate for the RHEMA Field POS application and its server contracts. It does not replace authenticated browser, live SQL Server, signed APK, payment-provider, or physical-device certification.

## Standard gate

Run from `apps/mobile` on Windows:

```powershell
npm run acceptance:mobile-pos
```

Or run the repository script directly:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/acceptance/Invoke-MobilePosAcceptance.ps1
```

The standard gate restores both lockfiles, rejects committed mobile binaries, checks Mobile POS and HQ TypeScript, runs the mobile tests and Expo Doctor, exports the Android Hermes bundle, performs a migration-aware API Release build, runs the focused Mobile POS API tests, and verifies idempotent SQL for all four Mobile POS migrations.

Evidence is written beneath the ignored `.artifacts/mobile-pos/acceptance/<UTC timestamp>/` directory. The JSON record contains the commit, stage status, timings, test result, and log paths.

## Z92S SDK packaging gate

Keep the vendor SDK outside the repository. Point the gate at the extracted directory that directly contains `libs/SmartPos_1.8.1_R231213.jar` and the two supported JNI ABI directories:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/acceptance/Invoke-MobilePosAcceptance.ps1 `
  -ZcsSdkDirectory 'C:\secure-sdk\SmartPos_1.8.1_R231213_SDK'
```

This mode performs a clean production Expo Android prebuild, requires backup and cleartext traffic to be disabled in the generated manifest, and rejects missing or hash-altered SDK artifacts. Vendor JAR/JNI files are copied only into the ignored generated Android project.

Add `-BuildNativeAndroid` only on a controlled host with the required Java and Android SDK toolchains. That option compiles a debug APK after the audited packaging check; it still does not certify printer output, scanner decode behavior, Android 14 firmware compatibility, signing, or upgrade behavior on a physical Z92S.

## Local iteration

`-SkipInstall` may be used only for a faster local rerun after both lockfiles have already been restored. The evidence records both dependency restore stages as skipped. Release evidence must use the standard gate without this switch.

## External acceptance still required

- Written permission to redistribute the SmartPos SDK in signed builds.
- Native Android compilation and signing on the controlled release toolchain.
- Printer, scanner, enrollment, revoke, upgrade, and recovery checks on a physical Android 14 Z92S.
- Supported Bluetooth ESC/POS printer models before that adapter is implemented.
- Authenticated HQ UI, API, SQL Server concurrency, Finance reconciliation, and disconnect/reconnect evidence in an authorized test environment.
- Selected Mobile Money/card provider contracts and sandbox certification.
