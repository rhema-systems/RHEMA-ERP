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

import { apiService as rawApiService } from '@/services/api.service';

interface ProcedurePdfViewerProps {
  fileUrl: string;
  fileName?: string | null;
}

function toApiEndpoint(url: string) {
  return url.startsWith('/api/') ? url.slice(4) : url;
}

export default function ProcedurePdfViewer({
  fileUrl,
  fileName,
}: ProcedurePdfViewerProps) {
  const [documentPath, setDocumentPath] = React.useState<string | null>(fileUrl);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const viewerId = React.useMemo(
    () => `procedure-pdf-${Math.random().toString(36).slice(2)}`,
    []
  );

  React.useEffect(() => {
    let cancelled = false;
    let objectUrl: string | null = null;

    if (!fileUrl.startsWith('/api/')) {
      setLoadError(null);
      setDocumentPath(fileUrl);
      return () => undefined;
    }

    setLoadError(null);
    setDocumentPath(null);

    const loadDocument = async () => {
      try {
        const blob = await rawApiService.downloadBlob(toApiEndpoint(fileUrl));
        if (cancelled) return;

        objectUrl = URL.createObjectURL(blob);
        setDocumentPath(objectUrl);
      } catch (error) {
        if (!cancelled) {
          setLoadError(
            error instanceof Error
              ? error.message
              : 'Unable to load the secured preview.'
          );
        }
      }
    };

    void loadDocument();

    return () => {
      cancelled = true;
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
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
      ) : documentPath ? (
        <PdfViewerComponent
          id={viewerId}
          documentPath={documentPath}
          resourceUrl="/syncfusion/ej2-pdfviewer-lib"
          enableToolbar
          enableNavigationToolbar
          enableThumbnail
          enableTextSearch
          enableTextSelection
          enableAnnotation={false}
          enableAnnotationToolbar={false}
          enableTextMarkupAnnotation={false}
          enableShapeAnnotation={false}
          enableStampAnnotations={false}
          enableStickyNotesAnnotation={false}
          enableInkAnnotation={false}
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
      ) : (
        <div className="p-4 text-sm text-muted-foreground">Loading secured preview...</div>
      )}
    </div>
  );
}
