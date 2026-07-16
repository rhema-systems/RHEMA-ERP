'use client';

import React from 'react';
import {
  Annotation,
  BookmarkView,
  Inject,
  LinkAnnotation,
  Magnification,
  Navigation,
  PdfViewerComponent,
  Print,
  TextSearch,
  TextSelection,
  ThumbnailView,
  Toolbar,
} from '@syncfusion/ej2-react-pdfviewer';

interface CentralDocumentPdfViewerProps {
  fileUrl: string;
  fileName?: string | null;
  enableAnnotations?: boolean;
}

export default function CentralDocumentPdfViewer({
  fileUrl,
  fileName,
  enableAnnotations = true,
}: CentralDocumentPdfViewerProps) {
  const viewerId = React.useMemo(
    () => `central-dms-pdf-${Math.random().toString(36).slice(2)}`,
    []
  );

  if (!fileUrl) {
    return null;
  }

  return (
    <div className="flex h-full min-h-[560px] flex-col overflow-hidden rounded-md border border-border bg-background">
      <div className="border-b border-border px-3 py-2 text-xs font-medium text-muted-foreground">
        {fileName || 'Central DMS PDF'}
      </div>
      <div className="min-h-0 flex-1">
        <PdfViewerComponent
          id={viewerId}
          documentPath={fileUrl}
          resourceUrl="https://cdn.syncfusion.com/ej2/34.1.30/dist/ej2-pdfviewer-lib"
          enableToolbar
          enableNavigationToolbar
          enableThumbnail
          enableTextSearch
          enableTextSelection
          enableAnnotation={enableAnnotations}
          enableAnnotationToolbar={enableAnnotations}
          enableTextMarkupAnnotation={enableAnnotations}
          enableShapeAnnotation={enableAnnotations}
          enableStampAnnotations={enableAnnotations}
          enableStickyNotesAnnotation={enableAnnotations}
          enableInkAnnotation={enableAnnotations}
          style={{ display: 'block', height: '100%', width: '100%' }}
        >
          <Inject
            services={[
              Toolbar,
              Magnification,
              Navigation,
              LinkAnnotation,
              BookmarkView,
              ThumbnailView,
              Print,
              TextSelection,
              TextSearch,
              Annotation,
            ]}
          />
        </PdfViewerComponent>
      </div>
    </div>
  );
}
