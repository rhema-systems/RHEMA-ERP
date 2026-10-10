import { describe, expect, it } from "vitest";

// The config plugin is CommonJS because Expo loads it directly during prebuild.
// eslint-disable-next-line @typescript-eslint/no-require-imports
const plugin = require("./with-mobile-security");

function manifest() {
  return { manifest: { application: [{ $: { "android:name": ".MainApplication" } }] } };
}

describe("Mobile POS Android security plugin", () => {
  it.each(["UAT", "PRODUCTION"])("disables backup and cleartext traffic for %s", environment => {
    const result = plugin.applyAndroidSecurityAttributes(manifest(), environment);
    expect(result.manifest.application[0].$).toMatchObject({
      "android:allowBackup": "false",
      "android:usesCleartextTraffic": "false",
    });
  });

  it("allows cleartext only in the TEST development build while still disabling backup", () => {
    const result = plugin.applyAndroidSecurityAttributes(manifest(), "TEST");
    expect(result.manifest.application[0].$).toMatchObject({
      "android:allowBackup": "false",
      "android:usesCleartextTraffic": "true",
    });
  });

  it("rejects an unknown environment instead of silently weakening the build", () => {
    expect(() => plugin.resolveEnvironment("staging")).toThrow("Unsupported RHEMA mobile environment");
  });
});
