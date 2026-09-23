import React from 'react';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import CentralDocumentPdfViewer from './CentralDocumentPdfViewer';

const { load, dataBind, destroy, callbacks } = vi.hoisted(() => ({
  load: vi.fn(),
  dataBind: vi.fn(),
  destroy: vi.fn(),
  callbacks: [] as Array<() => void>,
}));

vi.mock('@syncfusion/ej2-react-pdfviewer', async () => {
  const React = await import('react');
  const PdfViewerComponent = React.forwardRef<
    {
      load: typeof load;
      dataBind: typeof dataBind;
      destroy: typeof destroy;
    },
    {
      children?: React.ReactNode;
      id?: string;
      resourcesLoaded?: () => void;
    }
  >(function MockPdfViewerComponent({ children, id, resourcesLoaded }, ref) {
    React.useImperativeHandle(ref, () => ({ load, dataBind, destroy }));
    React.useEffect(() => {
      if (resourcesLoaded) {
        callbacks.push(resourcesLoaded);
      }
    }, [resourcesLoaded]);

    return React.createElement(
      'div',
      { id, 'data-testid': 'pdf-viewer' },
      children
    );
  });

  return {
    PdfViewerComponent,
    Inject: () => null,
    ExtractTextOption: {
      TextAndBounds: 'TextAndBounds',
    },
    Annotation: {},
    BookmarkView: {},
    FormDesigner: {},
    FormFields: {},
    LinkAnnotation: {},
    Magnification: {},
    Navigation: {},
    PageOrganizer: {},
    Print: {},
    TextSearch: {},
    TextSelection: {},
    ThumbnailView: {},
    Toolbar: {},
  };
});

afterEach(() => {
  cleanup();
  callbacks.length = 0;
  vi.clearAllMocks();
  vi.unstubAllGlobals();
});

describe('Central DMS PDF byte loading', () => {
  it('loads authorized bytes without re-fetching a blob URL or weakening network policy', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    const createObjectURL = vi.fn(() => 'blob:authorized-pdf');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', {
      ...URL,
      createObjectURL,
      revokeObjectURL,
    });
    const bytes = new Uint8Array([37, 80, 68, 70]);
    render(
      <CentralDocumentPdfViewer fileData={bytes} fileName="Contract.pdf" />
    );
    act(() => {
      callbacks[0]();
    });
    await waitFor(() =>
      expect(load).toHaveBeenCalledWith('blob:authorized-pdf', '')
    );
    expect(dataBind).not.toHaveBeenCalled();
    expect(createObjectURL).toHaveBeenCalledWith(expect.any(Blob));
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('retains URL-based loading for ordinary PDF sources', async () => {
    const blob = new Blob([new Uint8Array([37, 80, 68, 70])], {
      type: 'application/pdf',
    });
    const createObjectURL = vi.fn(() => 'blob:fetched-pdf');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', {
      ...URL,
      createObjectURL,
      revokeObjectURL,
    });
    const fetchMock = vi
      .fn()
      .mockResolvedValue({ ok: true, blob: async () => blob });
    vi.stubGlobal('fetch', fetchMock);
    render(<CentralDocumentPdfViewer fileUrl="/sample.pdf" />);
    act(() => {
      callbacks[0]();
    });
    await waitFor(() =>
      expect(load).toHaveBeenCalledWith('blob:fetched-pdf', '')
    );
    expect(dataBind).not.toHaveBeenCalled();
    expect(createObjectURL).toHaveBeenCalledWith(blob);
    expect(fetchMock).toHaveBeenCalledWith('/sample.pdf');
  });

  it('shows a URL fetch failure without loading invalid PDF bytes', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: false, status: 403 })
    );
    render(<CentralDocumentPdfViewer fileUrl="/restricted.pdf" />);
    act(() => {
      callbacks[0]();
    });
    expect(
      await screen.findByText('Unable to load PDF preview (403).')
    ).toBeInTheDocument();
    expect(load).not.toHaveBeenCalled();
  });
});
