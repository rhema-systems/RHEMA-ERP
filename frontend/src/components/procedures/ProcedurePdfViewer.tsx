'use client';

import React from 'react';
import {
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

interface ProcedurePdfViewerProps {
  fileUrl: string;
  fileName?: string | null;
}

export default function ProcedurePdfViewer({ fileUrl, fileName }: ProcedurePdfViewerProps) {
  const viewerId = React.useMemo(() => `procedure-pdf-${Math.random().toString(36).slice(2)}`, []);

  if (!fileUrl) {
    return null;
  }

  return (
    <div className="overflow-hidden rounded-md border border-border bg-background">
      <div className="border-b border-border px-3 py-2 text-xs font-medium text-muted-foreground">
        {fileName || 'Uploaded PDF'}
      </div>
      <PdfViewerComponent
        id={viewerId}
        documentPath={fileUrl}
        resourceUrl="https://cdn.syncfusion.com/ej2/34.1.30/dist/ej2-pdfviewer-lib"
        enableToolbar
        enableNavigationToolbar
        enableThumbnail
        enableTextSearch
        enableTextSelection
        style={{ display: 'block', height: '520px', width: '100%' }}
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
          ]}
        />
      </PdfViewerComponent>
    </div>
  );
}
