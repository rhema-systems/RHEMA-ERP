'use client';

import React from 'react';
import {
  BookmarkView,
  LinkAnnotation,
  Magnification,
  Navigation,
  PdfViewer,
  Print,
  TextSearch,
  TextSelection,
  ThumbnailView,
  Toolbar,
} from '@syncfusion/ej2-react-pdfviewer';

import { apiService as rawApiService } from '@/services/api.service';

interface ProcedurePdfViewerProps {
  fileUrl: string;
  fileName?: string | null;
  height?: string;
}

function toApiEndpoint(url: string) {
  return url.startsWith('/api/') ? url.slice(4) : url;
}

export default function ProcedurePdfViewer({
  fileUrl,
  fileName,
  height = '520px',
}: ProcedurePdfViewerProps) {
  const viewerHostRef = React.useRef<HTMLDivElement | null>(null);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const viewerId = React.useMemo(
    () => `procedure-pdf-${Math.random().toString(36).slice(2)}`,
    []
  );

  React.useEffect(() => {
    const host = viewerHostRef.current;
    if (!host || !fileUrl) return;
    let disposed = false;
    setLoadError(null);

    PdfViewer.Inject(
      Toolbar,
      Magnification,
      Navigation,
      LinkAnnotation,
      BookmarkView,
      ThumbnailView,
      Print,
      TextSelection,
      TextSearch
    );

    const loadDocument = async (viewer: PdfViewer) => {
      let documentBytes: Uint8Array;
      if (fileUrl.startsWith('/api/')) {
        const blob = await rawApiService.downloadBlob(toApiEndpoint(fileUrl));
        documentBytes = new Uint8Array(await blob.arrayBuffer());
      } else {
        const response = await fetch(fileUrl);
        if (!response.ok) {
          throw new Error(`Unable to load PDF preview (${response.status}).`);
        }
        documentBytes = new Uint8Array(await response.arrayBuffer());
      }

      if (!disposed) {
        viewer.load(documentBytes, '');
      }
    };

    const viewer = new PdfViewer({
      resourceUrl: `${window.location.origin}/syncfusion/ej2-pdfviewer-lib`,
      enableToolbar: true,
      enableNavigationToolbar: true,
      enableThumbnail: true,
      enableTextSearch: true,
      enableTextSelection: true,
      enableAnnotation: false,
      enableAnnotationToolbar: false,
      resourcesLoaded: () => {
        void loadDocument(viewer).catch((error) => {
          if (!disposed) {
            setLoadError(
              error instanceof Error
                ? error.message
                : 'Unable to load the secured preview.'
            );
          }
        });
      },
    });

    viewer.appendTo(host);

    return () => {
      disposed = true;
      viewer.destroy();
    };
  }, [fileUrl]);

  if (!fileUrl) {
    return null;
  }

  return (
    <div className="overflow-hidden rounded-md border border-border bg-background">
      <div className="border-b border-border px-3 py-2 text-xs font-medium text-muted-foreground">
        {fileName || 'Uploaded PDF'}
      </div>
      {loadError ? (
        <div className="p-4 text-sm text-destructive">{loadError}</div>
      ) : (
        <div
          id={viewerId}
          ref={viewerHostRef}
          style={{ display: 'block', height, width: '100%' }}
        />
      )}
    </div>
  );
}
