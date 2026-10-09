import { describe, expect, it, vi } from "vitest";
import { createBarcodeScan, TestBarcodeScannerAdapter } from "@/src/scanning/barcode";

describe("barcode scanner boundary", () => {
  it("normalizes camera and keyboard wedge terminators", () => {
    expect(createBarcodeScan("  0123456789012\r\n", "camera", "ean13", new Date("2026-10-09T12:00:00Z"))).toEqual({
      value: "0123456789012",
      source: "camera",
      symbology: "ean13",
      capturedAtUtc: "2026-10-09T12:00:00.000Z",
    });
  });

  it("rejects empty and oversized scanner payloads", () => {
    expect(() => createBarcodeScan("\r\n", "keyboard-wedge")).toThrow("did not return");
    expect(() => createBarcodeScan("x".repeat(101), "vendor")).toThrow("100 character");
  });

  it("keeps vendor integration behind a disposable adapter contract", async () => {
    const listener = vi.fn();
    const stop = await new TestBarcodeScannerAdapter(["ITEM-01"]).start(listener);
    await new Promise(resolve => setTimeout(resolve, 0));
    stop();

    expect(listener).toHaveBeenCalledOnce();
    expect(listener.mock.calls[0]?.[0]).toMatchObject({ value: "ITEM-01", source: "test" });
  });
});
