import { Directory, File, Paths } from "expo-file-system";
import * as Sharing from "expo-sharing";
import type { MobilePosSupportBundle } from "./support-bundle";

export async function shareMobilePosSupportBundle(bundle: MobilePosSupportBundle): Promise<string> {
  if (!await Sharing.isAvailableAsync()) {
    throw new Error("Support bundle sharing is not available on this device.");
  }
  const supportDirectory = new Directory(Paths.cache, "support");
  supportDirectory.create({ intermediates: true, idempotent: true });
  const safeReference = bundle.supportReference.replace(/[^A-Za-z0-9_-]/g, "-");
  const file = new File(supportDirectory, `rhema-mobile-support-${safeReference}.json`);
  if (file.exists) file.delete();
  file.create();
  file.write(JSON.stringify(bundle, null, 2));
  await Sharing.shareAsync(file.uri, {
    mimeType: "application/json",
    UTI: "public.json",
    dialogTitle: `Share support reference ${bundle.supportReference}`,
  });
  return file.uri;
}
