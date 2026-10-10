import { createRequire } from "node:module";
import { mkdtempSync, mkdirSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

const require = createRequire(import.meta.url);
const plugin = require("./with-zcs-smartpos.js") as {
  artifactDefinitions: Array<{ relativePath: string }>;
  isEnabled(value?: string): boolean;
  removeGeneratedGradleBlock(contents: string): string;
  resolveZcsSdkArtifacts(directory?: string): Array<{ key: string; actualHash: string }>;
};

describe("ZCS SmartPos config plugin", () => {
  it("enables vendor packaging only for explicit true values", () => {
    expect(plugin.isEnabled("true")).toBe(true);
    expect(plugin.isEnabled("1")).toBe(true);
    expect(plugin.isEnabled("off")).toBe(false);
    expect(plugin.isEnabled(undefined)).toBe(false);
  });

  it("removes a prior generated Gradle dependency block", () => {
    const source = "android {}\n// RHEMA_ZCS_SMARTPOS_START\ndependencies {\n implementation files('x.jar')\n}\n// RHEMA_ZCS_SMARTPOS_END\n";
    expect(plugin.removeGeneratedGradleBlock(source)).toBe("android {}\n");
  });

  it("rejects missing and altered external SDK artifacts", () => {
    expect(() => plugin.resolveZcsSdkArtifacts(undefined)).toThrow(/RHEMA_ZCS_SDK_DIR/);

    const root = mkdtempSync(join(tmpdir(), "rhema-zcs-plugin-"));
    for (const artifact of plugin.artifactDefinitions) {
      const file = join(root, artifact.relativePath);
      mkdirSync(join(file, ".."), { recursive: true });
      writeFileSync(file, "not-the-vendor-artifact");
    }
    expect(() => plugin.resolveZcsSdkArtifacts(root)).toThrow(/hash is invalid/);
  });
});
