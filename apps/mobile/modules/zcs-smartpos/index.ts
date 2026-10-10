import { requireOptionalNativeModule } from "expo-modules-core";

export interface ZcsSmartPosCapabilities {
  nativeModuleAvailable: boolean;
  sdkAvailable: boolean;
  printerAvailable: boolean;
  scannerAvailable: boolean;
  detail?: string;
}

export interface ZcsPrintOptions {
  textSize?: number;
  feedLines?: number;
}

interface ZcsSmartPosNativeModule {
  getCapabilities(): Promise<ZcsSmartPosCapabilities>;
  initialize(): Promise<number>;
  printText(text: string, options?: ZcsPrintOptions): Promise<number>;
  powerOnScanner(): Promise<void>;
  triggerScanner(): Promise<void>;
  stopScanner(): Promise<void>;
  powerOffScanner(): Promise<void>;
}

const nativeModule = requireOptionalNativeModule<ZcsSmartPosNativeModule>("ZcsSmartPos");

export function isZcsNativeModuleLinked(): boolean {
  return nativeModule !== null;
}

export async function getZcsSmartPosCapabilities(): Promise<ZcsSmartPosCapabilities> {
  if (!nativeModule) {
    return {
      nativeModuleAvailable: false,
      sdkAvailable: false,
      printerAvailable: false,
      scannerAvailable: false,
      detail: "The ZCS native module is unavailable in this build.",
    };
  }
  return nativeModule.getCapabilities();
}

function requireModule(): ZcsSmartPosNativeModule {
  if (!nativeModule) {
    throw new Error("The ZCS native module is unavailable. Install an Android build produced with the ZCS SDK enabled.");
  }
  return nativeModule;
}

export const ZcsSmartPos = {
  getCapabilities: getZcsSmartPosCapabilities,
  initialize: () => requireModule().initialize(),
  printText: (text: string, options?: ZcsPrintOptions) => requireModule().printText(text, options),
  powerOnScanner: () => requireModule().powerOnScanner(),
  triggerScanner: () => requireModule().triggerScanner(),
  stopScanner: () => requireModule().stopScanner(),
  powerOffScanner: () => requireModule().powerOffScanner(),
};
