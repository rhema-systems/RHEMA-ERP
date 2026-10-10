import { Directory, File, Paths } from "expo-file-system";
import * as Print from "expo-print";
import * as Sharing from "expo-sharing";
import { isZcsSmartPosAdapter, ZcsReceiptPrinterAdapter } from "@/src/hardware/zcs-smartpos";
import type { ReceiptPrintResult } from "@/src/printing/receipt-printer";
import { buildReceiptHtml, receiptPdfFileName } from "@/src/receipts/render";
import type { MobilePosPrintableReceipt } from "@/src/types/api";

export async function printReceiptAsync(receipt: MobilePosPrintableReceipt, configuredAdapterKey?: string): Promise<ReceiptPrintResult> {
  if (isZcsSmartPosAdapter(configuredAdapterKey)) {
    return new ZcsReceiptPrinterAdapter().print(receipt);
  }
  await Print.printAsync({ html: buildReceiptHtml(receipt) });
  return { adapterKey: "system-print", adapterLabel: "Android system print" };
}

export async function persistReceiptPdfAsync(receipt: MobilePosPrintableReceipt): Promise<string> {
  const generated = await Print.printToFileAsync({ html: buildReceiptHtml(receipt) });
  const receiptsDirectory = new Directory(Paths.document, "receipts");
  receiptsDirectory.create({ intermediates: true, idempotent: true });

  const source = new File(generated.uri);
  const target = new File(receiptsDirectory, receiptPdfFileName(receipt));
  if (target.exists) target.delete();
  source.copy(target);
  return target.uri;
}

export async function shareReceiptPdfAsync(receipt: MobilePosPrintableReceipt): Promise<string> {
  if (!await Sharing.isAvailableAsync()) {
    throw new Error("Receipt sharing is not available on this device.");
  }

  const uri = await persistReceiptPdfAsync(receipt);
  await Sharing.shareAsync(uri, {
    mimeType: "application/pdf",
    UTI: "com.adobe.pdf",
    dialogTitle: `Share receipt ${receipt.receiptKind === "SALE" ? receipt.invoiceNumber : receipt.localReference}`,
  });
  return uri;
}
