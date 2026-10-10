export type BarcodeScanSource = "camera" | "keyboard-wedge" | "vendor" | "test";

export interface BarcodeScan {
  value: string;
  source: BarcodeScanSource;
  symbology?: string;
  capturedAtUtc: string;
}

export type BarcodeScanListener = (scan: BarcodeScan) => void;

export interface BarcodeScannerAdapter {
  readonly source: BarcodeScanSource;
  start(listener: BarcodeScanListener): Promise<() => void>;
}

export function createBarcodeScan(
  rawValue: string,
  source: BarcodeScanSource,
  symbology?: string,
  capturedAt = new Date(),
): BarcodeScan {
  const value = rawValue.replace(/[\u0000-\u001f\u007f]/g, "").trim();
  if (!value) throw new Error("The scanner did not return a barcode value.");
  if (value.length > 100) throw new Error("The scanned barcode exceeds the 100 character catalogue limit.");

  return {
    value,
    source,
    symbology: symbology?.trim() || undefined,
    capturedAtUtc: capturedAt.toISOString(),
  };
}

export class TestBarcodeScannerAdapter implements BarcodeScannerAdapter {
  readonly source = "test" as const;

  constructor(private readonly values: string[]) {}

  async start(listener: BarcodeScanListener): Promise<() => void> {
    let active = true;
    queueMicrotask(() => {
      if (!active) return;
      for (const value of this.values) listener(createBarcodeScan(value, this.source));
    });
    return () => { active = false; };
  }
}
