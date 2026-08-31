'use client';

import React from 'react';
import {
  Annotation,
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
} from '@syncfusion/ej2-pdfviewer';

interface CentralDocumentPdfViewerProps {
  fileUrl: string;
  fileName?: string | null;
  enableAnnotations?: boolean;
}

export default function CentralDocumentPdfViewer({
  fileUrl,
  fileName,
  enableAnnotations = false,
}: CentralDocumentPdfViewerProps) {
  const viewerHostRef = React.useRef<HTMLDivElement | null>(null);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const viewerId = React.useMemo(
    () => `central-dms-pdf-${Math.random().toString(36).slice(2)}`,
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
      Annotation,
      BookmarkView,
      ThumbnailView,
      Print,
      TextSelection,
      TextSearch
    );

    const loadDocument = async (viewer: PdfViewer) => {
      const response = await fetch(fileUrl);
      if (!response.ok) {
        throw new Error(`Unable to load PDF preview (${response.status}).`);
      }

      const documentBytes = new Uint8Array(await response.arrayBuffer());
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
      enableAnnotation: enableAnnotations,
      enableAnnotationToolbar: enableAnnotations,
      enableTextMarkupAnnotation: enableAnnotations,
      enableShapeAnnotation: enableAnnotations,
      enableStampAnnotations: enableAnnotations,
      enableStickyNotesAnnotation: enableAnnotations,
      enableInkAnnotation: enableAnnotations,
      resourcesLoaded: () => {
        void loadDocument(viewer).catch((error) => {
          if (!disposed) {
            setLoadError(
              error instanceof Error
                ? error.message
                : 'Unable to load the PDF preview.'
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
  }, [enableAnnotations, fileUrl]);

  if (!fileUrl) {
    return null;
  }

  return (
    <div className="flex h-full min-h-[560px] flex-col overflow-hidden rounded-md border border-border bg-background">
      <div className="border-b border-border px-3 py-2 text-xs font-medium text-muted-foreground">
        {fileName || 'Central DMS PDF'}
      </div>
      <div className="relative min-h-0 flex-1">
        <div
          id={viewerId}
          ref={viewerHostRef}
          style={{ display: 'block', height: '100%', width: '100%' }}
        />
        {loadError ? (
          <div className="absolute inset-0 flex items-center justify-center bg-background p-6 text-sm text-destructive">
            {loadError}
          </div>
        ) : null}
      </div>
    </div>
  );
}
