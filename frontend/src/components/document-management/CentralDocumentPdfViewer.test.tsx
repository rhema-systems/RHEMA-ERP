import React from 'react';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import CentralDocumentPdfViewer from './CentralDocumentPdfViewer';

const { load, destroy, callbacks } = vi.hoisted(() => ({
  load: vi.fn(), destroy: vi.fn(), callbacks: [] as Array<() => void>,
}));
vi.mock('@syncfusion/ej2-pdfviewer', () => ({
  PdfViewer: class {
    static Inject() {}
    constructor(options: { resourcesLoaded: () => void }) { callbacks.push(options.resourcesLoaded); }
    appendTo() {}
    load = load;
    destroy = destroy;
  },
  Annotation: {}, BookmarkView: {}, LinkAnnotation: {}, Magnification: {}, Navigation: {}, Print: {},
  TextSearch: {}, TextSelection: {}, ThumbnailView: {}, Toolbar: {},
}));
afterEach(() => { cleanup(); callbacks.length = 0; vi.clearAllMocks(); vi.unstubAllGlobals(); });

describe('Central DMS PDF byte loading', () => {
  it('loads authorized bytes without re-fetching a blob URL or weakening network policy', async () => {
    const fetchMock = vi.fn(); vi.stubGlobal('fetch', fetchMock);
    const bytes = new Uint8Array([37, 80, 68, 70]);
    render(<CentralDocumentPdfViewer fileData={bytes} fileName="Contract.pdf" />);
    callbacks[0]();
    await waitFor(() => expect(load).toHaveBeenCalledWith(bytes, ''));
    expect(fetchMock).not.toHaveBeenCalled();
  });
  it('retains URL-based loading for ordinary PDF sources', async () => {
    const bytes = new Uint8Array([37, 80, 68, 70]);
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, arrayBuffer: async () => bytes.buffer });
    vi.stubGlobal('fetch', fetchMock);
    render(<CentralDocumentPdfViewer fileUrl="/sample.pdf" />);
    callbacks[0]();
    await waitFor(() => expect(load).toHaveBeenCalledWith(bytes, ''));
    expect(fetchMock).toHaveBeenCalledWith('/sample.pdf');
  });
  it('shows a URL fetch failure without loading invalid PDF bytes', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 403 }));
    render(<CentralDocumentPdfViewer fileUrl="/restricted.pdf" />);
    callbacks[0]();
    expect(await screen.findByText('Unable to load PDF preview (403).')).toBeInTheDocument();
    expect(load).not.toHaveBeenCalled();
  });
});
