import { describe, expect, it, vi } from "vitest";
import { adaptersFromZcsCapabilities } from "@/src/hardware/capabilities";

vi.mock("zcs-smartpos", () => ({ getZcsSmartPosCapabilities: vi.fn(), ZcsSmartPos: {} }));

describe("mobile hardware capability selection", () => {
  it("selects both Z92S adapters only when their packaged capabilities are present", () => {
    expect(adaptersFromZcsCapabilities({ nativeModuleAvailable: true, sdkAvailable: true, printerAvailable: true, scannerAvailable: true })).toEqual({
      printerAdapterKey: "zcs-smartpos",
      scannerAdapterKey: "zcs-smartpos",
    });
    expect(adaptersFromZcsCapabilities({ nativeModuleAvailable: true, sdkAvailable: true, printerAvailable: false, scannerAvailable: true })).toEqual({
      printerAdapterKey: "system-print",
      scannerAdapterKey: "zcs-smartpos",
    });
  });

  it("keeps portable fallbacks when the vendor SDK is absent", () => {
    expect(adaptersFromZcsCapabilities({ nativeModuleAvailable: true, sdkAvailable: false, printerAvailable: false, scannerAvailable: false })).toEqual({
      printerAdapterKey: "system-print",
      scannerAdapterKey: "camera-manual",
    });
  });
});
