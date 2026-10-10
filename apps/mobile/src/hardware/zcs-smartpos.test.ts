import { describe, expect, it, vi } from "vitest";
import { ZcsHardwareScannerAdapter, ZcsReceiptPrinterAdapter, type ZcsSmartPosNativeBridge } from "@/src/hardware/zcs-smartpos";
import type { MobilePosReceipt } from "@/src/types/api";

vi.mock("zcs-smartpos", () => ({ ZcsSmartPos: {} }));

function nativeBridge(overrides: Partial<ZcsSmartPosNativeBridge> = {}): ZcsSmartPosNativeBridge {
  return {
    getCapabilities: vi.fn(async () => ({
      nativeModuleAvailable: true,
      sdkAvailable: true,
      printerAvailable: true,
      scannerAvailable: true,
    })),
    initialize: vi.fn(async () => 0),
    printText: vi.fn(async () => 0),
    powerOnScanner: vi.fn(async () => undefined),
    triggerScanner: vi.fn(async () => undefined),
    stopScanner: vi.fn(async () => undefined),
    powerOffScanner: vi.fn(async () => undefined),
    ...overrides,
  };
}

describe("ZCS SmartPos adapters", () => {
  it("controls the hardware scanner and normalizes keyboard-wedge output", async () => {
    const native = nativeBridge();
    const adapter = new ZcsHardwareScannerAdapter(native);
    const listener = vi.fn();

    const dispose = await adapter.start(listener);
    await adapter.trigger();
    const scan = adapter.acceptKeyboardWedge("  6291100032104\n");
    dispose();
    await vi.waitFor(() => expect(native.powerOffScanner).toHaveBeenCalledOnce());

    expect(native.powerOnScanner).toHaveBeenCalledOnce();
    expect(native.triggerScanner).toHaveBeenCalledOnce();
    expect(scan).toMatchObject({ value: "6291100032104", source: "vendor" });
    expect(listener).toHaveBeenCalledWith(scan);
  });

  it("reports a missing packaged SDK before invoking the scanner", async () => {
    const native = nativeBridge({
      getCapabilities: vi.fn(async () => ({
        nativeModuleAvailable: true,
        sdkAvailable: false,
        printerAvailable: false,
        scannerAvailable: false,
        detail: "SDK not packaged",
      })),
    });
    await expect(new ZcsHardwareScannerAdapter(native).start(vi.fn())).rejects.toThrow("SDK not packaged");
  });

  it("renders and submits a canonical receipt to the built-in printer", async () => {
    const native = nativeBridge();
    const receipt = {
      receiptKind: "SALE",
      copyType: "ORIGINAL",
      copyNumber: 0,
      tenantName: "RHEMA ERP",
      storeName: "Accra Shop",
      tillNumber: "TILL-1",
      tillSessionNumber: "SHIFT-1",
      invoiceNumber: "INV-1",
      customerName: "Walk-in",
      customerCode: "WALK-IN",
      cashierName: "Ama",
      occurredAtUtc: "2026-10-10T10:00:00Z",
      localReference: "MOB-1",
      currencyCode: "GHS",
      subTotal: 10,
      discountAmount: 0,
      taxAmount: 0,
      totalAmount: 10,
      lines: [{ description: "Item", quantity: 1, unitOfMeasureCode: "Each", unitPrice: 10, discountAmount: 0, lineTotal: 10 }],
      tenders: [{ paymentMethodName: "Cash", amount: 10, paymentNumber: "PAY-1" }],
      qrReference: "RHEMA|INV-1",
    } as MobilePosReceipt;

    await expect(new ZcsReceiptPrinterAdapter(native).print(receipt)).resolves.toEqual({
      adapterKey: "zcs-smartpos",
      adapterLabel: "Z92S built-in printer",
    });
    expect(native.printText).toHaveBeenCalledWith(expect.stringContaining("INV-1"), { textSize: 24, feedLines: 4 });
  });
});
