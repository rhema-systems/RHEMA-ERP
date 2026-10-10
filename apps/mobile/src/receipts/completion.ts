export interface CompletedReceiptOutput<TResult, TReceipt> {
  result: TResult;
  receipt?: TReceipt;
  outputError?: unknown;
  outputStage?: "load" | "persist";
}

export async function completeWithCanonicalReceipt<TResult, TReceipt>(actions: {
  complete: () => Promise<TResult>;
  loadReceipt: (result: TResult) => Promise<TReceipt>;
  persistReceipt: (receipt: TReceipt) => Promise<unknown>;
}): Promise<CompletedReceiptOutput<TResult, TReceipt>> {
  const result = await actions.complete();
  let receipt: TReceipt;
  try {
    receipt = await actions.loadReceipt(result);
  } catch (outputError) {
    return { result, outputError, outputStage: "load" };
  }
  try {
    await actions.persistReceipt(receipt);
  } catch (outputError) {
    return { result, receipt, outputError, outputStage: "persist" };
  }
  return { result, receipt };
}
