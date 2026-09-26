type PdfSaveWindow = Window & {
  showSaveFilePicker?: (options: {
    suggestedName: string;
    types: { description: string; accept: Record<string, string[]> }[];
  }) => Promise<{ createWritable: () => Promise<{
    write: (data: Blob) => Promise<void>;
    close: () => Promise<void>;
    abort: () => Promise<void>;
  }> }>;
};

/** Call directly from a user gesture, using bytes already obtained through the protected API. */
export async function savePdfCopy(bytes: Uint8Array, fileName: string): Promise<'saved' | 'cancelled'> {
  const host = window as PdfSaveWindow;
  if (!host.showSaveFilePicker) {
    throw new Error('This browser cannot open a Save As dialog. Open this page in a browser that supports saving files, such as Chrome or Edge.');
  }
  if (!bytes.byteLength) throw new Error('Load the PDF before saving a copy.');
  let handle;
  try {
    handle = await host.showSaveFilePicker({
      suggestedName: fileName.toLowerCase().endsWith('.pdf') ? fileName : `${fileName}.pdf`,
      types: [{ description: 'PDF document', accept: { 'application/pdf': ['.pdf'] } }],
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return 'cancelled';
    throw error;
  }
  const writable = await handle.createWritable();
  try {
    await writable.write(new Blob([new Uint8Array(bytes)], { type: 'application/pdf' }));
    await writable.close();
    return 'saved';
  } catch (error) {
    await writable.abort().catch(() => undefined);
    throw error;
  }
}
