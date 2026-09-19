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
  fileUrl?: string | null;
  fileData?: Uint8Array | null;
  fileName?: string | null;
  enableAnnotations?: boolean;
  annotationStateJson?: string | null;
}

export interface CentralDocumentPdfViewerHandle {
  exportAnnotationState: () => Promise<string | null>;
  exportAnnotatedPdfBlob: () => Promise<Blob | null>;
}

const CentralDocumentPdfViewer = React.forwardRef<
  CentralDocumentPdfViewerHandle,
  CentralDocumentPdfViewerProps
>(function CentralDocumentPdfViewer(
{
  fileUrl,
  fileData,
  fileName,
  enableAnnotations = false,
  annotationStateJson,
},
ref
) {
  const viewerHostRef = React.useRef<HTMLDivElement | null>(null);
  const viewerInstanceRef = React.useRef<PdfViewer | null>(null);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const viewerId = React.useMemo(
    () => `central-dms-pdf-${Math.random().toString(36).slice(2)}`,
    []
  );

  React.useImperativeHandle(ref, () => ({
    exportAnnotationState: async () => {
      const viewer = viewerInstanceRef.current as
        | (PdfViewer & {
            exportAnnotationsAsObject?: () => unknown;
            exportAnnotation?: (format?: string) => unknown;
            exportAnnotations?: () => unknown;
          })
        | null;
      if (!viewer) return null;

      const exported =
        typeof viewer.exportAnnotationsAsObject === 'function'
          ? viewer.exportAnnotationsAsObject()
          : typeof viewer.exportAnnotation === 'function'
            ? viewer.exportAnnotation('Json')
            : typeof viewer.exportAnnotations === 'function'
              ? viewer.exportAnnotations()
              : null;
      const resolved = exported instanceof Promise ? await exported : exported;
      if (!resolved) return null;
      return typeof resolved === 'string'
        ? resolved
        : JSON.stringify(resolved);
    },
    exportAnnotatedPdfBlob: async () => {
      const viewer = viewerInstanceRef.current as
        | (PdfViewer & {
            saveAsBlob?: () => Promise<Blob>;
          })
        | null;
      if (!viewer || typeof viewer.saveAsBlob !== 'function') return null;
      return viewer.saveAsBlob();
    },
  }));

  React.useEffect(() => {
    const host = viewerHostRef.current;
    if (!host || (!fileUrl && !fileData)) return;
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
      let documentBytes = fileData;
      if (!documentBytes && fileUrl) {
        const response = await fetch(fileUrl);
        if (!response.ok) {
          throw new Error(`Unable to load PDF preview (${response.status}).`);
        }
        documentBytes = new Uint8Array(await response.arrayBuffer());
      }
      if (!disposed && documentBytes) {
        viewer.load(documentBytes, '');
        if (enableAnnotations && annotationStateJson?.trim()) {
          window.setTimeout(() => {
            if (disposed) return;
            const annotationViewer = viewer as PdfViewer & {
              importAnnotations?: (data: string) => void;
              importAnnotation?: (data: string) => void;
            };
            if (typeof annotationViewer.importAnnotations === 'function') {
              annotationViewer.importAnnotations(annotationStateJson);
            } else if (typeof annotationViewer.importAnnotation === 'function') {
              annotationViewer.importAnnotation(annotationStateJson);
            }
          }, 250);
        }
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
    viewerInstanceRef.current = viewer;

    return () => {
      disposed = true;
      viewerInstanceRef.current = null;
      viewer.destroy();
    };
  }, [annotationStateJson, enableAnnotations, fileUrl, fileData]);

  if (!fileUrl && !fileData) {
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
});

export default CentralDocumentPdfViewer;
