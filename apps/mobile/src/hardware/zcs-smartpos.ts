import { ZcsSmartPos, type ZcsSmartPosCapabilities } from "zcs-smartpos";
import { buildReceiptText } from "@/src/printing/receipt-text";
import type { PrinterCapability, ReceiptPrinterAdapter, ReceiptPrintResult } from "@/src/printing/receipt-printer";
import { createBarcodeScan, type BarcodeScan, type BarcodeScanListener, type BarcodeScannerAdapter } from "@/src/scanning/barcode";
import type { MobilePosPrintableReceipt } from "@/src/types/api";

export const zcsSmartPosAdapterKey = "zcs-smartpos";

export interface ZcsSmartPosNativeBridge {
  getCapabilities(): Promise<ZcsSmartPosCapabilities>;
  initialize(): Promise<number>;
  printText(text: string, options?: { textSize?: number; feedLines?: number }): Promise<number>;
  powerOnScanner(): Promise<void>;
  triggerScanner(): Promise<void>;
  stopScanner(): Promise<void>;
  powerOffScanner(): Promise<void>;
}

export function isZcsSmartPosAdapter(value?: string): boolean {
  return value?.trim().toLowerCase() === zcsSmartPosAdapterKey;
}

export class ZcsReceiptPrinterAdapter implements ReceiptPrinterAdapter {
  readonly key = zcsSmartPosAdapterKey;

  constructor(private readonly native: ZcsSmartPosNativeBridge = ZcsSmartPos) {}

  async getCapability(): Promise<PrinterCapability> {
    const capability = await this.native.getCapabilities();
    return {
      key: this.key,
      label: "Z92S built-in printer",
      available: capability.nativeModuleAvailable && capability.sdkAvailable && capability.printerAvailable,
      builtIn: true,
      detail: capability.detail,
    };
  }

  async print(receipt: MobilePosPrintableReceipt): Promise<ReceiptPrintResult> {
    const capability = await this.getCapability();
    if (!capability.available) throw new Error(capability.detail || "The Z92S built-in printer is unavailable.");
    await this.native.initialize();
    await this.native.printText(buildReceiptText(receipt), { textSize: 24, feedLines: 4 });
    return { adapterKey: this.key, adapterLabel: capability.label };
  }
}

/**
 * The SmartPos scanner writes decoded data to the focused Android text input.
 * This adapter owns power/trigger lifecycle and converts that wedge value into
 * the same normalized event used by camera and test scanners.
 */
export class ZcsHardwareScannerAdapter implements BarcodeScannerAdapter {
  readonly source = "vendor" as const;
  private listener: BarcodeScanListener | null = null;
  private active = false;

  constructor(private readonly native: ZcsSmartPosNativeBridge = ZcsSmartPos) {}

  async start(listener: BarcodeScanListener): Promise<() => void> {
    const capability = await this.native.getCapabilities();
    if (!capability.nativeModuleAvailable || !capability.sdkAvailable || !capability.scannerAvailable) {
      throw new Error(capability.detail || "The Z92S hardware scanner is unavailable.");
    }
    this.listener = listener;
    await this.native.initialize();
    await this.native.powerOnScanner();
    this.active = true;
    return () => { void this.stop(); };
  }

  async trigger(): Promise<void> {
    if (!this.active) throw new Error("Start the Z92S scanner before triggering it.");
    await this.native.triggerScanner();
  }

  acceptKeyboardWedge(rawValue: string): BarcodeScan {
    if (!this.active || !this.listener) throw new Error("The Z92S scanner is not active.");
    const scan = createBarcodeScan(rawValue, this.source);
    this.listener(scan);
    return scan;
  }

  async stop(): Promise<void> {
    if (!this.active) return;
    this.active = false;
    this.listener = null;
    try {
      await this.native.stopScanner();
    } finally {
      await this.native.powerOffScanner();
    }
  }
}
