import { getZcsSmartPosCapabilities, type ZcsSmartPosCapabilities } from "zcs-smartpos";
import { zcsSmartPosAdapterKey } from "@/src/hardware/zcs-smartpos";

export interface MobileHardwareAdapters {
  printerAdapterKey: string;
  scannerAdapterKey: string;
}

export function adaptersFromZcsCapabilities(capability: ZcsSmartPosCapabilities): MobileHardwareAdapters {
  return {
    printerAdapterKey: capability.sdkAvailable && capability.printerAvailable ? zcsSmartPosAdapterKey : "system-print",
    scannerAdapterKey: capability.sdkAvailable && capability.scannerAvailable ? zcsSmartPosAdapterKey : "camera-manual",
  };
}

export async function detectMobileHardwareAdapters(): Promise<MobileHardwareAdapters> {
  try {
    return adaptersFromZcsCapabilities(await getZcsSmartPosCapabilities());
  } catch {
    return { printerAdapterKey: "system-print", scannerAdapterKey: "camera-manual" };
  }
}
