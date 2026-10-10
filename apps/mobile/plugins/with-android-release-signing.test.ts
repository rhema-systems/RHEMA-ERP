import { createRequire } from "node:module";
import { describe, expect, it } from "vitest";

const require = createRequire(import.meta.url);
const plugin = require("./with-android-release-signing.js") as {
  applyReleaseSigningGradle(contents: string): string;
  removeGeneratedGradleBlock(contents: string): string;
};

describe("Android release signing config plugin", () => {
  it("adds a fail-closed release-task guard and external signing configuration", () => {
    const generated = plugin.applyReleaseSigningGradle("android { buildTypes { release {} } }");

    expect(generated).toContain("RHEMA_ANDROID_SIGNING_ENABLED");
    expect(generated).toContain("RHEMA_ANDROID_KEYSTORE_PATH");
    expect(generated).toContain("RHEMA_ANDROID_KEYSTORE_PASSWORD");
    expect(generated).toContain("RHEMA_ANDROID_KEY_ALIAS");
    expect(generated).toContain("RHEMA_ANDROID_KEY_PASSWORD");
    expect(generated).toContain("releaseRequested");
    expect(generated).toContain("signingConfig signingConfigs.rhemaRelease");
    expect(generated).not.toContain("storePassword 'android'");
  });

  it("is idempotent across repeated clean-prebuild configuration", () => {
    const first = plugin.applyReleaseSigningGradle("android {}");
    const second = plugin.applyReleaseSigningGradle(first);

    expect(second.match(/RHEMA_ANDROID_RELEASE_SIGNING_START/g)).toHaveLength(1);
    expect(second.match(/RHEMA_ANDROID_RELEASE_SIGNING_END/g)).toHaveLength(1);
  });

  it("does not interpolate credential values into generated Gradle source", () => {
    const priorPassword = process.env.RHEMA_ANDROID_KEYSTORE_PASSWORD;
    process.env.RHEMA_ANDROID_KEYSTORE_PASSWORD = "must-never-be-rendered";
    try {
      expect(plugin.applyReleaseSigningGradle("android {}"))
        .not.toContain("must-never-be-rendered");
    } finally {
      if (priorPassword === undefined) delete process.env.RHEMA_ANDROID_KEYSTORE_PASSWORD;
      else process.env.RHEMA_ANDROID_KEYSTORE_PASSWORD = priorPassword;
    }
  });
});
