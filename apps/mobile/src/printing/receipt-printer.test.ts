import { describe, expect, it } from "vitest";
import { TestReceiptPrinterAdapter } from "@/src/printing/receipt-printer";
import type { MobilePosReceipt } from "@/src/types/api";

describe("receipt printer contract", () => {
  it("provides a deterministic fake without native hardware", async () => {
    const adapter = new TestReceiptPrinterAdapter();
    const receipt = { receiptKind: "SALE", invoiceNumber: "INV-1" } as MobilePosReceipt;

    expect((await adapter.getCapability()).available).toBe(true);
    await expect(adapter.print(receipt)).resolves.toEqual({ adapterKey: "test-printer", adapterLabel: "Test printer" });
    expect(adapter.printed).toEqual([receipt]);
  });
});
