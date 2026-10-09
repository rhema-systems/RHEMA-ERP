import { Directory, File, Paths } from "expo-file-system";
import * as Print from "expo-print";
import * as Sharing from "expo-sharing";
import { buildReceiptHtml, receiptPdfFileName } from "@/src/receipts/render";
import type { MobilePosReceipt } from "@/src/types/api";

export async function printReceiptAsync(receipt: MobilePosReceipt): Promise<void> {
  await Print.printAsync({ html: buildReceiptHtml(receipt) });
}

export async function persistReceiptPdfAsync(receipt: MobilePosReceipt): Promise<string> {
  const generated = await Print.printToFileAsync({ html: buildReceiptHtml(receipt) });
  const receiptsDirectory = new Directory(Paths.document, "receipts");
  receiptsDirectory.create({ intermediates: true, idempotent: true });

  const source = new File(generated.uri);
  const target = new File(receiptsDirectory, receiptPdfFileName(receipt));
  if (target.exists) target.delete();
  source.copy(target);
  return target.uri;
}

export async function shareReceiptPdfAsync(receipt: MobilePosReceipt): Promise<string> {
  if (!await Sharing.isAvailableAsync()) {
    throw new Error("Receipt sharing is not available on this device.");
  }

  const uri = await persistReceiptPdfAsync(receipt);
  await Sharing.shareAsync(uri, {
    mimeType: "application/pdf",
    UTI: "com.adobe.pdf",
    dialogTitle: `Share receipt ${receipt.invoiceNumber}`,
  });
  return uri;
}
