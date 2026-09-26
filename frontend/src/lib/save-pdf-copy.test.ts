import { afterEach, expect, it, vi } from 'vitest';
import { savePdfCopy } from './save-pdf-copy';

afterEach(() => vi.unstubAllGlobals());
const bytes = new Uint8Array([37, 80, 68, 70]);
it('saves already-authorized PDF bytes and confirms only after the file closes', async () => {
  const stream = { write: vi.fn().mockResolvedValue(undefined), close: vi.fn().mockResolvedValue(undefined), abort: vi.fn() };
  const picker = vi.fn().mockResolvedValue({ createWritable: async () => stream });
  vi.stubGlobal('showSaveFilePicker', picker);
  expect(await savePdfCopy(bytes, 'IPC-UAT.pdf')).toBe('saved');
  expect(picker).toHaveBeenCalledWith(expect.objectContaining({ suggestedName: 'IPC-UAT.pdf' }));
  expect(stream.write.mock.calls[0][0]).toMatchObject({ size: 4, type: 'application/pdf' });
  expect(stream.close).toHaveBeenCalledOnce();
  expect(stream.abort).not.toHaveBeenCalled();
});
it('does not report a cancelled picker as a successful save', async () => {
  vi.stubGlobal('showSaveFilePicker', vi.fn().mockRejectedValue(new DOMException('Cancelled', 'AbortError')));
  expect(await savePdfCopy(bytes, 'IPC-UAT.pdf')).toBe('cancelled');
});
it('aborts and surfaces a write failure without reporting success', async () => {
  const stream = { write: vi.fn().mockRejectedValue(new Error('Disk full')), close: vi.fn(), abort: vi.fn().mockResolvedValue(undefined) };
  vi.stubGlobal('showSaveFilePicker', vi.fn().mockResolvedValue({ createWritable: async () => stream }));
  await expect(savePdfCopy(bytes, 'IPC-UAT.pdf')).rejects.toThrow('Disk full');
  expect(stream.abort).toHaveBeenCalledOnce();
  expect(stream.close).not.toHaveBeenCalled();
});
it('explains unsupported save dialogs instead of silently claiming a download', async () => {
  vi.stubGlobal('showSaveFilePicker', undefined);
  await expect(savePdfCopy(bytes, 'IPC-UAT.pdf')).rejects.toThrow('cannot open a Save As dialog');
});
