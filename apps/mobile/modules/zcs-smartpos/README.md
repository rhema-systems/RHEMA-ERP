# ZCS SmartPos native bridge

This local Expo module adapts the built-in printer and `HQrsanner` hardware in
the ZCS SmartPos 1.8.1 SDK supplied for the Z92S. The proprietary JAR and JNI
libraries are deliberately excluded from Git because the supplied archive did
not contain redistribution terms.

Ordinary development builds compile the reflection-based bridge without the
vendor SDK. A Z92S build must point at an extracted, approved copy of
`SmartPos_1.8.1_R231213_SDK`:

```powershell
$env:RHEMA_ZCS_ENABLED = 'true'
$env:RHEMA_ZCS_SDK_DIR = 'C:\secure-sdk\SmartPos_1.8.1_R231213_SDK'
npm run prebuild:android
```

The config plugin refuses to package the SDK unless the JAR, ARM64 JNI library,
and ARMv7 JNI library match the audited SHA-256 hashes recorded in
`plugins/with-zcs-smartpos.js`. The generated `android/` directory is ignored,
so the proprietary files do not enter source control.

Scanner output follows the vendor demo's keyboard-wedge contract. The native
bridge powers and triggers the scanner while the sale screen focuses the
catalogue input; the terminating Enter key uses the normal barcode search path.

Source and automated checks establish the integration boundary only. A signed
APK still requires written redistribution approval and certification on a
physical Z92S running the target Android 14 firmware.
