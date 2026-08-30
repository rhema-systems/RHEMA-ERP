import html2canvas from 'html2canvas';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { exportProcurementDocumentPdf } from './procurement-document-output';

const mocks = vi.hoisted(() => ({
  addImage: vi.fn(),
  addPage: vi.fn(),
  save: vi.fn(),
}));

vi.mock('html2canvas', () => ({
  default: vi.fn(),
}));

vi.mock('jspdf', () => ({
  jsPDF: class {
    internal = {
      pageSize: {
        getWidth: () => 210,
        getHeight: () => 297,
      },
    };

    addImage = mocks.addImage;
    addPage = mocks.addPage;
    save = mocks.save;
  },
}));

const mockedHtml2Canvas = vi.mocked(html2canvas);

const createCanvas = () => ({
  width: 1000,
  height: 500,
  toDataURL: vi.fn(() => 'data:image/png;base64,pdf-test'),
} as unknown as HTMLCanvasElement);

describe('procurement document PDF output', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('uses the standard canvas renderer when the document styles are supported', async () => {
    mockedHtml2Canvas.mockResolvedValueOnce(createCanvas());

    const element = document.createElement('main');
    await exportProcurementDocumentPdf(element, 'PO 2026/0002');

    expect(mockedHtml2Canvas).toHaveBeenCalledTimes(1);
    expect(mockedHtml2Canvas).toHaveBeenCalledWith(element, expect.not.objectContaining({
      foreignObjectRendering: true,
    }));
    expect(mocks.save).toHaveBeenCalledWith('PO-2026-0002.pdf');
  });

  it('retries with browser-native rendering when html2canvas cannot parse oklch', async () => {
    mockedHtml2Canvas
      .mockRejectedValueOnce(new Error('Attempting to parse an unsupported color function "oklch"'))
      .mockResolvedValueOnce(createCanvas());

    const element = document.createElement('main');
    await exportProcurementDocumentPdf(element, 'PO-2026-0002');

    expect(mockedHtml2Canvas).toHaveBeenCalledTimes(2);
    expect(mockedHtml2Canvas).toHaveBeenNthCalledWith(2, element, expect.objectContaining({
      backgroundColor: '#ffffff',
      foreignObjectRendering: true,
      scale: 1.5,
      useCORS: true,
    }));
    expect(mocks.save).toHaveBeenCalledWith('PO-2026-0002.pdf');
  });

  it('does not hide unrelated rendering failures', async () => {
    mockedHtml2Canvas.mockRejectedValueOnce(new Error('Unable to load required image'));

    await expect(exportProcurementDocumentPdf(
      document.createElement('main'),
      'PO-2026-0002'
    )).rejects.toThrow('Unable to load required image');

    expect(mockedHtml2Canvas).toHaveBeenCalledTimes(1);
    expect(mocks.save).not.toHaveBeenCalled();
  });
});
