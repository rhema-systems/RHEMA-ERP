import type { MobilePosPrintableReceipt } from "@/src/types/api";

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
  print(receipt: MobilePosPrintableReceipt): Promise<ReceiptPrintResult>;
}

export class TestReceiptPrinterAdapter implements ReceiptPrinterAdapter {
  readonly key = "test-printer";
  readonly printed: MobilePosPrintableReceipt[] = [];

  async getCapability(): Promise<PrinterCapability> {
    return { key: this.key, label: "Test printer", available: true, builtIn: false };
  }

  async print(receipt: MobilePosPrintableReceipt): Promise<ReceiptPrintResult> {
    this.printed.push(receipt);
    return { adapterKey: this.key, adapterLabel: "Test printer" };
  }
}
