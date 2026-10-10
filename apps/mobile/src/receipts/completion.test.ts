import { describe, expect, it, vi } from "vitest";
import { completeWithCanonicalReceipt } from "@/src/receipts/completion";

describe("completed transaction receipt output", () => {
  it("lets a completion failure escape so the caller may apply its offline policy", async () => {
    const loadReceipt = vi.fn();
    await expect(completeWithCanonicalReceipt({
      complete: async () => { throw new Error("transport failed"); },
      loadReceipt,
      persistReceipt: vi.fn(),
    })).rejects.toThrow("transport failed");
    expect(loadReceipt).not.toHaveBeenCalled();
  });

  it("returns a completed result when receipt loading fails and does not resubmit the transaction", async () => {
    const complete = vi.fn(async () => ({ id: "sale-1" }));
    const output = await completeWithCanonicalReceipt({
      complete,
      loadReceipt: async () => { throw new Error("receipt unavailable"); },
      persistReceipt: vi.fn(),
    });
    expect(output).toMatchObject({ result: { id: "sale-1" }, outputStage: "load" });
    expect(output.receipt).toBeUndefined();
    expect(complete).toHaveBeenCalledTimes(1);
  });

  it("keeps a loaded receipt visible when local persistence fails", async () => {
    const receipt = { receiptId: "sale-1" };
    const output = await completeWithCanonicalReceipt({
      complete: async () => ({ id: "sale-1" }),
      loadReceipt: async () => receipt,
      persistReceipt: async () => { throw new Error("disk full"); },
    });
    expect(output).toMatchObject({ result: { id: "sale-1" }, receipt, outputStage: "persist" });
  });
});
