import type { MobilePosReceipt } from "@/src/types/api";

export interface PrinterCapability {
  key: string;
  label: string;
  available: boolean;
  builtIn: boolean;
  detail?: string;
}

export interface ReceiptPrintResult {
  adapterKey: string;
  adapterLabel: string;
}

export interface ReceiptPrinterAdapter {
  readonly key: string;
  getCapability(): Promise<PrinterCapability>;
  print(receipt: MobilePosReceipt): Promise<ReceiptPrintResult>;
}

export class TestReceiptPrinterAdapter implements ReceiptPrinterAdapter {
  readonly key = "test-printer";
  readonly printed: MobilePosReceipt[] = [];

  async getCapability(): Promise<PrinterCapability> {
    return { key: this.key, label: "Test printer", available: true, builtIn: false };
  }

  async print(receipt: MobilePosReceipt): Promise<ReceiptPrintResult> {
    this.printed.push(receipt);
    return { adapterKey: this.key, adapterLabel: "Test printer" };
  }
}
