'use client';

import React from 'react';
import {
  Annotation,
  BookmarkView,
  ExtractTextOption,
  FormDesigner,
  FormFields,
  Inject,
  LinkAnnotation,
  Magnification,
  Navigation,
  PageOrganizer,
  PdfViewerComponent,
  Print,
  TextSearch,
  TextSelection,
  ThumbnailView,
  Toolbar,
  type AnnotationToolbarItem,
  type ToolbarItem,
} from '@syncfusion/ej2-react-pdfviewer';

import { registerSyncfusionLicense } from '@/lib/syncfusion-license';
import { authService } from '@/services/auth';

const ANNOTATOR_COLORS = [
  '#F59E0B',
  '#10B981',
  '#3B82F6',
  '#EC4899',
  '#8B5CF6',
  '#EF4444',
  '#14B8A6',
  '#6366F1',
  '#84CC16',
  '#F97316',
];

function getCurrentAnnotatorName() {
  const user = authService.getStoredUser();
  const fullName = [user?.firstName, user?.lastName]
    .filter(Boolean)
    .join(' ')
    .trim();

  return fullName || user?.username || user?.email || 'Unknown user';
}

function getAnnotatorColor(value: string) {
  let hash = 0;
  for (let index = 0; index < value.length; index += 1) {
    hash = (hash << 5) - hash + value.charCodeAt(index);
    hash |= 0;
  }

  return ANNOTATOR_COLORS[Math.abs(hash) % ANNOTATOR_COLORS.length];
}

function toPdfBlob(bytes: Uint8Array) {
  const buffer = new ArrayBuffer(bytes.byteLength);
  new Uint8Array(buffer).set(bytes);
  return new Blob([buffer], { type: 'application/pdf' });
}

type TextSelectionCapableViewer = PdfViewerComponent & {
  enableTextSelection?: boolean;
  extractTextOption?: ExtractTextOption;
  interactionMode?: 'TextSelection' | 'Pan';
  textSelectionModule?: {
    enableTextSelectionMode?: () => void;
  };
  toolbarModule?: {
    updateInteractionTools?: (isTextSelection: boolean) => void;
  };
  viewerBase?: {
    initiateTextSelectMode?: () => void;
    isPanMode?: boolean;
    isTextSelectionDisabled?: boolean;
  };
};

type AnnotationCapableViewer = PdfViewerComponent & {
  exportAnnotationAsObject?: () => unknown;
  exportAnnotationsAsObject?: () => unknown;
  exportAnnotation?: (format?: string) => unknown;
  exportAnnotations?: () => unknown;
  importAnnotations?: (data: string) => void;
  importAnnotation?: (data: string) => void;
};

function isPdfViewerAnnotationUiTarget(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  return Boolean(
    target.closest(
      [
        '.e-pv-annotation-toolbar',
        '.e-pv-annotation-popup',
        '.e-pv-stamp-popup',
        '.e-pv-signature-dialog',
        '.e-pv-comment-panel-container',
        '.e-pv-properties-window',
        '.e-pv-annotation-context-menu',
        '.e-pv-annotation-color-container',
        '.e-pv-annotation-stroke-container',
        '.e-pv-annotation-thickness-container',
        '.e-pv-annotation-opacity-container',
        '[id*="_annotation"]',
      ].join(',')
    )
  );
}

function isPdfViewerTextMarkupAnnotationTarget(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  const toolText = [
    target.getAttribute('aria-label'),
    target.getAttribute('title'),
    target.id,
    target.textContent,
  ]
    .filter(Boolean)
    .join(' ');

  return /highlight|underline|strikethrough|squiggly/i.test(toolText);
}

function isPdfViewerTextSelectionUiTarget(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  return Boolean(
    target.closest(
      [
        '.e-pv-text-select-tool-icon',
        '[id*="_selectTool"]',
        '[aria-label*="Selection"]',
        '[title*="Selection"]',
      ].join(',')
    )
  );
}

function forceTextSelectionMode(viewer: PdfViewerComponent | null) {
  if (!viewer) return;

  const selectableViewer = viewer as TextSelectionCapableViewer;
  selectableViewer.enableTextSelection = true;
  selectableViewer.extractTextOption = ExtractTextOption.TextAndBounds;
  selectableViewer.interactionMode = 'TextSelection';
  if (selectableViewer.viewerBase) {
    selectableViewer.viewerBase.isPanMode = false;
    selectableViewer.viewerBase.isTextSelectionDisabled = false;
  }
  selectableViewer.textSelectionModule?.enableTextSelectionMode?.();
  selectableViewer.toolbarModule?.updateInteractionTools?.(true);
}

function restoreTextLayerSelection(viewerId: string) {
  const viewerElement = document.getElementById(viewerId);
  if (!viewerElement) return;

  viewerElement
    .querySelectorAll(
      [
        '.e-pv-viewer-container',
        '.e-pv-page-container',
        '.e-pv-page-div',
        '.e-pv-text-layer',
        '.e-disable-text-selection',
      ].join(',')
    )
    .forEach((element) => {
      element.classList.remove('e-disable-text-selection');
      element.classList.add('e-enable-text-selection');
    });

  viewerElement
    .querySelectorAll('.e-pv-text-selection-none')
    .forEach((element) => {
      element.classList.remove('e-pv-text-selection-none');
      element.classList.add('e-enable-text-selection');
    });
}

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
  const viewerInstanceRef = React.useRef<PdfViewerComponent | null>(null);
  const objectUrlRef = React.useRef<string | null>(null);
  const textSelectionModeRef = React.useRef(true);
  const selectionRestoreTimersRef = React.useRef<number[]>([]);
  const annotationImportTimersRef = React.useRef<number[]>([]);
  const lastImportedAnnotationKeyRef = React.useRef<string | null>(null);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const [resourcesReady, setResourcesReady] = React.useState(false);
  const viewerId = React.useMemo(
    () => `central-dms-pdf-${Math.random().toString(36).slice(2)}`,
    []
  );
  const annotatorName = React.useMemo(() => getCurrentAnnotatorName(), []);
  const annotatorColor = React.useMemo(
    () => getAnnotatorColor(annotatorName),
    [annotatorName]
  );

  const restoreTextSelectionIfActive = React.useCallback(() => {
    if (!textSelectionModeRef.current) return;

    forceTextSelectionMode(viewerInstanceRef.current);
    restoreTextLayerSelection(viewerId);
  }, [viewerId]);

  const scheduleTextSelectionRestore = React.useCallback(
    (delays: number[] = [0, 100, 300, 750, 1500]) => {
      selectionRestoreTimersRef.current.forEach((timerId) => {
        window.clearTimeout(timerId);
      });
      selectionRestoreTimersRef.current = delays.map((delay) =>
        window.setTimeout(() => {
          window.requestAnimationFrame(restoreTextSelectionIfActive);
        }, delay)
      );
    },
    [restoreTextSelectionIfActive]
  );

  const ensureTextSelectionMode = React.useCallback(() => {
    textSelectionModeRef.current = true;
    restoreTextSelectionIfActive();
    scheduleTextSelectionRestore();
  }, [restoreTextSelectionIfActive, scheduleTextSelectionRestore]);

  const suspendTextSelectionMode = React.useCallback(() => {
    textSelectionModeRef.current = false;
  }, []);

  const clearAnnotationImportTimers = React.useCallback(() => {
    annotationImportTimersRef.current.forEach((timerId) => {
      window.clearTimeout(timerId);
    });
    annotationImportTimersRef.current = [];
  }, []);

  const importAnnotationStateIfReady = React.useCallback(() => {
    if (!enableAnnotations || !annotationStateJson?.trim()) {
      return;
    }

    const viewer = viewerInstanceRef.current as AnnotationCapableViewer | null;
    if (!viewer) {
      return;
    }

    const importKey = `${objectUrlRef.current || 'document'}:${annotationStateJson}`;
    if (lastImportedAnnotationKeyRef.current === importKey) {
      return;
    }

    if (typeof viewer.importAnnotations === 'function') {
      viewer.importAnnotations(annotationStateJson);
      lastImportedAnnotationKeyRef.current = importKey;
    } else if (typeof viewer.importAnnotation === 'function') {
      viewer.importAnnotation(annotationStateJson);
      lastImportedAnnotationKeyRef.current = importKey;
    }
  }, [annotationStateJson, enableAnnotations]);

  const scheduleAnnotationImport = React.useCallback(
    (delays: number[] = [0, 250, 750, 1500]) => {
      clearAnnotationImportTimers();
      if (!enableAnnotations || !annotationStateJson?.trim()) {
        return;
      }

      annotationImportTimersRef.current = delays.map((delay) =>
        window.setTimeout(importAnnotationStateIfReady, delay)
      );
    },
    [
      annotationStateJson,
      clearAnnotationImportTimers,
      enableAnnotations,
      importAnnotationStateIfReady,
    ]
  );

  React.useEffect(
    () => () => {
      selectionRestoreTimersRef.current.forEach((timerId) => {
        window.clearTimeout(timerId);
      });
      selectionRestoreTimersRef.current = [];
      clearAnnotationImportTimers();
    },
    [clearAnnotationImportTimers]
  );

  React.useImperativeHandle(ref, () => ({
    exportAnnotationState: async () => {
      const viewer = viewerInstanceRef.current as
        | AnnotationCapableViewer
        | null;
      if (!viewer) return null;

      const exported =
        typeof viewer.exportAnnotationAsObject === 'function'
          ? viewer.exportAnnotationAsObject()
          : typeof viewer.exportAnnotationsAsObject === 'function'
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
        | (PdfViewerComponent & {
            saveAsBlob?: () => Promise<Blob>;
          })
        | null;
      if (!viewer || typeof viewer.saveAsBlob !== 'function') return null;
      return viewer.saveAsBlob();
    },
  }));

  React.useEffect(() => {
    if (!resourcesReady || (!fileUrl && !fileData)) return;
    let disposed = false;
    setLoadError(null);

    registerSyncfusionLicense();

    const loadDocument = async () => {
      const viewer = viewerInstanceRef.current;
      if (!viewer) return;
      let documentBlob = fileData
        ? toPdfBlob(fileData)
        : null;
      if (!documentBlob && fileUrl) {
        const response = await fetch(fileUrl);
        if (!response.ok) {
          throw new Error(`Unable to load PDF preview (${response.status}).`);
        }
        documentBlob = await response.blob();
      }
      if (!disposed && documentBlob) {
        if (objectUrlRef.current) {
          URL.revokeObjectURL(objectUrlRef.current);
        }
        const objectUrl = URL.createObjectURL(documentBlob);
        objectUrlRef.current = objectUrl;
        lastImportedAnnotationKeyRef.current = null;

        textSelectionModeRef.current = true;
        viewer.load(objectUrl, '');
      }
    };

    void loadDocument().catch((error) => {
      if (!disposed) {
        setLoadError(
          error instanceof Error
            ? error.message
            : 'Unable to load the PDF preview.'
        );
      }
    });

    return () => {
      disposed = true;
    };
  }, [
    fileUrl,
    fileData,
    resourcesReady,
  ]);

  React.useEffect(() => {
    if (resourcesReady) {
      scheduleAnnotationImport([250, 750, 1500, 2500]);
    }
  }, [resourcesReady, scheduleAnnotationImport]);

  React.useEffect(() => {
    if (!resourcesReady) return;

    scheduleTextSelectionRestore([250, 750, 1500, 2500]);
  }, [resourcesReady, scheduleTextSelectionRestore]);

  React.useEffect(
    () => () => {
      if (objectUrlRef.current) {
        URL.revokeObjectURL(objectUrlRef.current);
        objectUrlRef.current = null;
      }
    },
    []
  );

  if (!fileUrl && !fileData) {
    return null;
  }

  const toolbarItems: ToolbarItem[] = enableAnnotations
    ? [
        'PageNavigationTool',
        'MagnificationTool',
        'SelectionTool',
        'SearchOption',
        'UndoRedoTool',
        'AnnotationEditTool',
        'CommentTool',
        'PrintOption',
        'DownloadOption',
      ]
    : [
        'PageNavigationTool',
        'MagnificationTool',
        'SelectionTool',
        'SearchOption',
        'PrintOption',
        'DownloadOption',
      ];
  const annotationToolbarItems: AnnotationToolbarItem[] = enableAnnotations
    ? [
        'HighlightTool',
        'UnderlineTool',
        'StrikethroughTool',
        'SquigglyTool',
        'FreeTextAnnotationTool',
        'ShapeTool',
        'StampAnnotationTool',
        'HandWrittenSignatureTool',
        'InkAnnotationTool',
        'ColorEditTool',
        'StrokeColorEditTool',
        'ThicknessEditTool',
        'OpacityEditTool',
        'AnnotationDeleteTool',
        'CommentPanelTool',
      ]
    : [];

  return (
    <div
      className="central-dms-pdf-viewer flex h-full min-h-[560px] flex-col overflow-hidden rounded-md border border-border bg-background"
    >
      <div className="border-b border-border px-3 py-2 text-xs font-medium text-muted-foreground">
        {fileName || 'Central DMS PDF'}
      </div>
      <div
        className="relative min-h-0 flex-1"
        onMouseDownCapture={(event) => {
          if (isPdfViewerAnnotationUiTarget(event.target)) {
            if (isPdfViewerTextMarkupAnnotationTarget(event.target)) {
              ensureTextSelectionMode();
            } else {
              suspendTextSelectionMode();
            }
            return;
          }

          if (
            textSelectionModeRef.current ||
            isPdfViewerTextSelectionUiTarget(event.target)
          ) {
            window.setTimeout(ensureTextSelectionMode, 0);
          }
        }}
        onPointerDownCapture={(event) => {
          if (isPdfViewerAnnotationUiTarget(event.target)) {
            if (isPdfViewerTextMarkupAnnotationTarget(event.target)) {
              ensureTextSelectionMode();
            } else {
              suspendTextSelectionMode();
            }
            return;
          }

          if (
            textSelectionModeRef.current ||
            isPdfViewerTextSelectionUiTarget(event.target)
          ) {
            window.setTimeout(ensureTextSelectionMode, 0);
          }
        }}
        onMouseUpCapture={(event) => {
          if (isPdfViewerAnnotationUiTarget(event.target)) {
            return;
          }

          if (
            textSelectionModeRef.current ||
            isPdfViewerTextSelectionUiTarget(event.target)
          ) {
            window.setTimeout(ensureTextSelectionMode, 0);
          }
        }}
      >
        <PdfViewerComponent
          id={viewerId}
          ref={viewerInstanceRef}
          resourceUrl={`${window.location.origin}/syncfusion/ej2-pdfviewer-lib`}
          enableToolbar
          enableNavigationToolbar
          enableThumbnail
          extractTextOption={ExtractTextOption.TextAndBounds}
          interactionMode="TextSelection"
          enableTextSearch
          enableTextSelection
          enableAnnotation={enableAnnotations}
          enableAnnotationToolbar={enableAnnotations}
          enableTextMarkupAnnotation={enableAnnotations}
          enableShapeAnnotation={enableAnnotations}
          enableStampAnnotations={enableAnnotations}
          enableStickyNotesAnnotation={enableAnnotations}
          enableInkAnnotation={enableAnnotations}
          enableHandwrittenSignature={enableAnnotations}
          enableFormFields={enableAnnotations}
          enableFormDesigner={enableAnnotations}
          enableFormDesignerToolbar={enableAnnotations}
          annotationSettings={{
            author: annotatorName,
          }}
          highlightSettings={{
            author: annotatorName,
            color: annotatorColor,
            opacity: 0.45,
          }}
          underlineSettings={{
            author: annotatorName,
            color: annotatorColor,
            opacity: 0.85,
          }}
          strikethroughSettings={{
            author: annotatorName,
            color: annotatorColor,
            opacity: 0.85,
          }}
          squigglySettings={{
            author: annotatorName,
            color: annotatorColor,
            opacity: 0.85,
          }}
          freeTextSettings={{
            author: annotatorName,
            fontColor: annotatorColor,
          }}
          inkAnnotationSettings={{
            author: annotatorName,
            strokeColor: annotatorColor,
            thickness: 2,
          }}
          lineSettings={{
            author: annotatorName,
            strokeColor: annotatorColor,
            fillColor: '#ffffff00',
            thickness: 2,
          }}
          arrowSettings={{
            author: annotatorName,
            strokeColor: annotatorColor,
            fillColor: annotatorColor,
            thickness: 2,
          }}
          rectangleSettings={{
            author: annotatorName,
            strokeColor: annotatorColor,
            fillColor: `${annotatorColor}22`,
            thickness: 2,
          }}
          circleSettings={{
            author: annotatorName,
            strokeColor: annotatorColor,
            fillColor: `${annotatorColor}22`,
            thickness: 2,
          }}
          polygonSettings={{
            author: annotatorName,
            strokeColor: annotatorColor,
            fillColor: `${annotatorColor}22`,
            thickness: 2,
          }}
          stickyNotesSettings={{
            author: annotatorName,
          }}
          stampSettings={{
            author: annotatorName,
          }}
          toolbarSettings={{
            showTooltip: true,
            toolbarItems,
            annotationToolbarItems,
          }}
          resourcesLoaded={() => {
            setResourcesReady(true);
          }}
          documentLoad={() => {
            ensureTextSelectionMode();
            scheduleAnnotationImport([0, 250, 750]);
            window.setTimeout(ensureTextSelectionMode, 0);
          }}
          pageRenderComplete={() => {
            if (textSelectionModeRef.current) {
              ensureTextSelectionMode();
            }
            scheduleAnnotationImport([0, 300]);
          }}
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
              FormFields,
              FormDesigner,
              PageOrganizer,
            ]}
          />
        </PdfViewerComponent>
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
