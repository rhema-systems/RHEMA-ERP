import { describe, expect, it } from "vitest";
import { createSupportReference } from "@/src/diagnostics/support-reference";

describe("support references", () => {
  it("creates a compact reference without including failure details", () => {
    const reference = createSupportReference(1_760_000_000_000, 0.5);
    expect(reference).toMatch(/^MOBILE-[0-9A-Z]+-[0-9A-Z]{4}$/);
    expect(reference).not.toContain("password");
    expect(reference).toBe(createSupportReference(1_760_000_000_000, 0.5));
  });
});
