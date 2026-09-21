'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import {
  CheckCircle2,
  Download,
  ExternalLink,
  FileWarning,
  Loader2,
  RefreshCw,
  Save,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { apiService as rawApiService } from '@/services/api.service';
import { documentManagementService } from '@/services/document-management.service';
import { savePdfCopy } from '@/lib/save-pdf-copy';
import type { CentralDocumentPdfViewerHandle } from '@/components/document-management/CentralDocumentPdfViewer';

const CentralDocumentPdfViewer = dynamic(
  () => import('@/components/document-management/CentralDocumentPdfViewer'),
  { ssr: false }
);

export interface CentralDocumentViewerFile {
  documentRecordId?: string | null;
  versionId?: string | null;
  fileUploadRecordId?: string | null;
  title: string;
  fileName?: string | null;
  repositoryPath?: string | null;
  renditionPath?: string | null;
  externalDocumentUrl?: string | null;
  contentType?: string | null;
  sourceLabel?: string | null;
  version?: string | null;
  /** Already generated PDF bytes. Never used to override a protected/linked document source. */
  pdfData?: Uint8Array | null;
  annotationStateJson?: string | null;
}

interface CentralDocumentViewerDialogProps {
  file: CentralDocumentViewerFile | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  enableAnnotations?: boolean;
  enableSaveCopy?: boolean;
  onGenerateRendition?: (
    file: CentralDocumentViewerFile
  ) => Promise<CentralDocumentViewerFile | null>;
  onDownload?: (
    file: CentralDocumentViewerFile,
    format: 'pdf' | 'word'
  ) => Promise<void>;
  onSaveAnnotations?: (
    file: CentralDocumentViewerFile,
    annotationStateJson: string | null,
    annotatedPdfBlob: Blob | null
  ) => Promise<CentralDocumentViewerFile | null | void>;
}

function isPdfSource(file: CentralDocumentViewerFile, path?: string | null) {
  const contentType = file.contentType?.toLowerCase() || '';
  const fileName = file.fileName?.toLowerCase() || '';
  const sourcePath = path?.toLowerCase() || '';

  return (
    contentType.includes('pdf') ||
    fileName.endsWith('.pdf') ||
    sourcePath.includes('.pdf')
  );
}

function normalizeViewerUrl(path?: string | null) {
  const value = path?.trim();
  if (!value) return null;

  if (/^https?:\/\//i.test(value) || value.startsWith('/')) {
    return value;
  }

  return null;
}

function getOriginalUrl(file: CentralDocumentViewerFile) {
  return (
    normalizeViewerUrl(file.externalDocumentUrl) ||
    normalizeViewerUrl(file.repositoryPath)
  );
}

function toApiEndpoint(url: string) {
  return url.startsWith('/api/') ? url.slice(4) : url;
}

async function blobToUint8Array(blob: Blob) {
  return new Uint8Array(await blob.arrayBuffer());
}

function resolvePdfView(file: CentralDocumentViewerFile | null) {
  if (!file) {
    return { url: null, originalUrl: null, status: 'none' as const };
  }

  if (file.pdfData?.byteLength && !file.documentRecordId && !file.versionId &&
      !file.fileUploadRecordId && !file.repositoryPath && !file.renditionPath && !file.externalDocumentUrl) {
    return { url: null, originalUrl: null, status: 'generated' as const };
  }

  const renditionUrl = normalizeViewerUrl(file.renditionPath);
  if (renditionUrl) {
    return {
      url: renditionUrl,
      originalUrl: getOriginalUrl(file),
      status: 'rendition' as const,
    };
  }

  const repositoryUrl = normalizeViewerUrl(file.repositoryPath);
  const externalUrl = normalizeViewerUrl(file.externalDocumentUrl);
  const directUrl = repositoryUrl || externalUrl;

  if (directUrl && isPdfSource(file, directUrl)) {
    return {
      url: directUrl,
      originalUrl: directUrl,
      status: 'direct' as const,
    };
  }

  return {
    url: null,
    originalUrl: directUrl,
    status: directUrl ? ('conversion-required' as const) : ('missing' as const),
  };
}

function isSyncfusionPopupTarget(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  return Boolean(
    target.closest(
      [
        '.e-popup',
        '.e-dialog',
        '.e-dropdown-popup',
        '.e-tooltip-wrap',
        '.e-pv-signature-dialog',
        '.e-pv-stamp-popup',
        '.e-pv-annotation-popup',
        '.e-pdfviewer',
      ].join(',')
    )
  );
}

export function CentralDocumentViewerDialog({
  file,
  open,
  onOpenChange,
  enableAnnotations = true,
  enableSaveCopy = false,
  onGenerateRendition,
  onDownload,
  onSaveAnnotations,
}: CentralDocumentViewerDialogProps) {
  const annotationsEnabled = enableAnnotations;
  const pdfViewerRef = React.useRef<CentralDocumentPdfViewerHandle | null>(
    null
  );
  const [localFile, setLocalFile] =
    React.useState<CentralDocumentViewerFile | null>(file);
  const [previewData, setPreviewData] = React.useState<Uint8Array | null>(
    null
  );
  const [isLoadingPreview, setIsLoadingPreview] = React.useState(false);
  const [isGenerating, setIsGenerating] = React.useState(false);
  const [downloadingFormat, setDownloadingFormat] = React.useState<
    'pdf' | 'word' | null
  >(null);
  const [openingSource, setOpeningSource] = React.useState(false);
  const [isSavingAnnotations, setIsSavingAnnotations] = React.useState(false);
  const [generateError, setGenerateError] = React.useState<string | null>(null);
  const [downloadError, setDownloadError] = React.useState<string | null>(null);
  const [annotationSaveMessage, setAnnotationSaveMessage] =
    React.useState<string | null>(null);
  const [savingCopy, setSavingCopy] = React.useState(false);
  const [saveCopyMessage, setSaveCopyMessage] = React.useState<string | null>(null);

  React.useEffect(() => {
    setLocalFile(file);
    setGenerateError(null);
    setDownloadError(null);
    setAnnotationSaveMessage(null);
    setDownloadingFormat(null);
    setSaveCopyMessage(null);
  }, [file]);

  const view = resolvePdfView(localFile);
  const securePreviewRequired = Boolean(
    (localFile?.documentRecordId || view.url?.startsWith('/api/')) &&
    (localFile?.versionId || view.url?.startsWith('/api/'))
  );
  const viewerUrl = securePreviewRequired ? null : view.url;
  const title = localFile?.title || 'Document preview';
  const canGenerateRendition =
    view.status === 'conversion-required' &&
    Boolean(localFile?.documentRecordId && localFile?.versionId) &&
    Boolean(onGenerateRendition);
  const canDownloadVersion =
    Boolean(localFile?.documentRecordId && localFile?.versionId) &&
    Boolean(onDownload);
  const canSaveAnnotations =
    annotationsEnabled &&
    Boolean(localFile?.documentRecordId && localFile?.versionId) &&
    Boolean(onSaveAnnotations);

  React.useEffect(() => {
    let cancelled = false;
    setPreviewData(null);
    const currentFile = localFile;
    const secureApiUrl = view.url?.startsWith('/api/') ? view.url : null;

    if (open && view.status === 'generated' && currentFile?.pdfData) {
      setPreviewData(currentFile.pdfData);
      setIsLoadingPreview(false);
      return () => undefined;
    }

    if (!open || (!currentFile?.documentRecordId && !secureApiUrl)) {
      setIsLoadingPreview(false);
      return () => undefined;
    }

    const shouldFetchSecurePreview =
      Boolean(currentFile?.versionId) ||
      Boolean(secureApiUrl) ||
      view.url?.startsWith('/api/document-management/');

    if (!shouldFetchSecurePreview) {
      setIsLoadingPreview(false);
      return () => undefined;
    }

    setIsLoadingPreview(true);
    const loadPreview = async () => {
      try {
        let blob: Blob;
        if (secureApiUrl) {
          blob = await rawApiService.downloadBlob(toApiEndpoint(secureApiUrl));
        } else if (currentFile?.documentRecordId && currentFile.versionId) {
          blob = await documentManagementService.downloadVersionFile(
            currentFile.documentRecordId,
            currentFile.versionId,
            'pdf'
          );
        } else if (currentFile?.documentRecordId) {
          blob = await documentManagementService.downloadRecordContent(
            currentFile.documentRecordId
          );
        } else {
          return;
        }

        // Pass already-authorized bytes directly to the PDF renderer. Re-fetching
        // a blob URL is unnecessary and fails under rehearsal's network policy.
        const bytes = new Uint8Array(await blob.arrayBuffer());
        if (cancelled) return;
        setPreviewData(bytes);
      } catch (error) {
        if (!cancelled) {
          setDownloadError(
            error instanceof Error
              ? error.message
              : 'Unable to load the secured document preview.'
          );
        }
      } finally {
        if (!cancelled) {
          setIsLoadingPreview(false);
        }
      }
    };

    void loadPreview();

    return () => {
      cancelled = true;
    };
  }, [
    open,
    localFile?.documentRecordId,
    localFile?.versionId,
    localFile?.repositoryPath,
    localFile?.renditionPath,
    localFile?.pdfData,
    view.status,
    view.url,
  ]);

  const handleGenerateRendition = async () => {
    if (!localFile || !onGenerateRendition) return;

    setIsGenerating(true);
    setGenerateError(null);
    try {
      const updatedFile = await onGenerateRendition(localFile);
      if (updatedFile) {
        setLocalFile(updatedFile);
      }
    } catch (error) {
      setGenerateError(
        error instanceof Error
          ? error.message
          : 'Unable to generate the PDF rendition.'
      );
    } finally {
      setIsGenerating(false);
    }
  };

  const handleDownload = async (format: 'pdf' | 'word') => {
    if (!localFile || !onDownload) return;

    setDownloadingFormat(format);
    setDownloadError(null);
    try {
      await onDownload(localFile, format);
    } catch (error) {
      setDownloadError(
        error instanceof Error
          ? error.message
          : `Unable to download the ${format === 'pdf' ? 'PDF' : 'Word'} file.`
      );
    } finally {
      setDownloadingFormat(null);
    }
  };

  const handleSaveAnnotations = async () => {
    if (!localFile || !onSaveAnnotations) return;

    setIsSavingAnnotations(true);
    setDownloadError(null);
    setAnnotationSaveMessage(null);
    try {
      const annotationStateJson =
        (await pdfViewerRef.current?.exportAnnotationState()) ?? null;
      const annotatedPdfBlob =
        (await pdfViewerRef.current?.exportAnnotatedPdfBlob()) ?? null;
      const updatedFile = await onSaveAnnotations(
        localFile,
        annotationStateJson,
        annotatedPdfBlob
      );
      if (updatedFile) {
        setLocalFile(updatedFile);
      }
      if (annotatedPdfBlob) {
        const savedPreviewData = await blobToUint8Array(annotatedPdfBlob);
        window.setTimeout(() => {
          setPreviewData(savedPreviewData);
        }, 0);
      }
      setAnnotationSaveMessage(
        updatedFile?.version
          ? `Annotations saved as ${updatedFile.version}.`
          : 'Annotations saved.'
      );
    } catch (error) {
      setDownloadError(
        error instanceof Error
          ? error.message
          : 'Unable to save the document annotations.'
      );
    } finally {
      setIsSavingAnnotations(false);
    }
  };

  const handleOpenSourceFile = async () => {
    if (!localFile || !view.originalUrl) return;

    const sourceUrl = view.originalUrl;
    setOpeningSource(true);
    setDownloadError(null);
    try {
      if (sourceUrl.startsWith('/api/')) {
        const blob = await rawApiService.downloadBlob(toApiEndpoint(sourceUrl));
        const objectUrl = URL.createObjectURL(blob);
        const opened = window.open(objectUrl, '_blank', 'noopener,noreferrer');
        if (!opened) {
          const link = document.createElement('a');
          link.href = objectUrl;
          link.download = localFile.fileName || `${title}.pdf`;
          document.body.appendChild(link);
          link.click();
          link.remove();
        }
        window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
        return;
      }

      window.open(sourceUrl, '_blank', 'noopener,noreferrer');
    } catch (error) {
      setDownloadError(
        error instanceof Error
          ? error.message
          : 'Unable to open the source file.'
      );
    } finally {
      setOpeningSource(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange} modal={false}>
      <DialogContent
        className="flex h-[92vh] max-h-[92vh] w-[96vw] max-w-7xl flex-col overflow-hidden p-0"
        onInteractOutside={(event) => {
          if (isSyncfusionPopupTarget(event.target)) {
            event.preventDefault();
          }
        }}
      >
        <DialogHeader className="border-b px-5 py-4 pr-12">
          <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
            <div className="min-w-0">
              <DialogTitle className="truncate">{title}</DialogTitle>
              <DialogDescription className="mt-1 truncate">
                {localFile?.sourceLabel ||
                  localFile?.fileName ||
                  'Central DMS document'}
              </DialogDescription>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              {canDownloadVersion ? (
                <>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => void handleDownload('pdf')}
                    disabled={downloadingFormat !== null}
                  >
                    <Download
                      className={`mr-2 h-4 w-4 ${
                        downloadingFormat === 'pdf' ? 'animate-spin' : ''
                      }`}
                    />
                    PDF
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => void handleDownload('word')}
                    disabled={downloadingFormat !== null}
                  >
                    <Download
                      className={`mr-2 h-4 w-4 ${
                        downloadingFormat === 'word' ? 'animate-spin' : ''
                      }`}
                    />
                    Word
                  </Button>
                </>
              ) : null}
              {localFile?.version ? (
                <Badge variant="outline">{localFile.version}</Badge>
              ) : null}
              {canSaveAnnotations ? (
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  onClick={() => void handleSaveAnnotations()}
                  disabled={isSavingAnnotations || isLoadingPreview}
                >
                  {isSavingAnnotations ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Save className="mr-2 h-4 w-4" />
                  )}
                  Save annotations
                </Button>
              ) : null}
              {view.status === 'rendition' ? (
                <Badge variant="secondary">PDF rendition</Badge>
              ) : null}
              {view.status === 'direct' ? (
                <Badge variant="secondary">PDF source</Badge>
              ) : null}
              {!annotationsEnabled ? (
                <Badge variant="outline">Read only</Badge>
              ) : null}
            </div>
          </div>
          {enableSaveCopy && !annotationsEnabled && previewData && (
            <Button variant="outline" disabled={savingCopy} onClick={async () => {
              setSavingCopy(true);
              setDownloadError(null);
              setSaveCopyMessage(null);
              try {
                const result = await savePdfCopy(previewData, localFile?.fileName || `${title}.pdf`);
                if (result === 'saved') setSaveCopyMessage('PDF saved successfully.');
              } catch (error) {
                setDownloadError(error instanceof Error ? error.message : 'The PDF could not be saved. Please try again.');
              } finally { setSavingCopy(false); }
            }}>{savingCopy ? 'Saving PDF...' : 'Save PDF as...'}</Button>
          )}
          {saveCopyMessage && <p role="status" className="mt-2 text-sm">{saveCopyMessage}</p>}
          {downloadError ? (
            <p role="alert" className="mt-2 text-sm text-destructive">{downloadError}</p>
          ) : null}
          {annotationSaveMessage ? (
            <div className="mt-2 flex items-center gap-2 text-sm text-emerald-700">
              <CheckCircle2 className="h-4 w-4" />
              {annotationSaveMessage}
            </div>
          ) : null}
        </DialogHeader>

        <div className="min-h-0 flex-1 overflow-hidden bg-background p-4">
          {previewData || viewerUrl ? (
            <CentralDocumentPdfViewer
              ref={pdfViewerRef}
              fileUrl={viewerUrl}
              fileData={previewData}
              fileName={localFile?.fileName || title}
              enableAnnotations={annotationsEnabled}
              annotationStateJson={localFile?.annotationStateJson}
            />
          ) : (
            <div className="flex h-full min-h-[420px] items-center justify-center rounded-md border bg-muted/30 p-6">
              <div className="max-w-lg text-center">
                {view.status === 'conversion-required' ? (
                  <RefreshCw className="mx-auto h-8 w-8 text-primary" />
                ) : (
                  <FileWarning className="mx-auto h-8 w-8 text-muted-foreground" />
                )}
                <h3 className="mt-4 text-base font-semibold">
                  {view.status === 'conversion-required'
                    ? 'PDF rendition required'
                    : isLoadingPreview
                      ? 'Loading secure preview'
                    : 'No viewable file linked'}
                </h3>
                <p className="mt-2 text-sm text-muted-foreground">
                  {isLoadingPreview
                    ? 'Fetching the protected file through Central DMS permissions.'
                    : view.status === 'conversion-required'
                    ? 'Generate a PDF rendition to open this document in the PDF viewer.'
                    : 'Link a PDF source or PDF rendition before previewing the document.'}
                </p>
                {canGenerateRendition ? (
                  <Button
                    type="button"
                    className="mt-4"
                    onClick={handleGenerateRendition}
                    disabled={isGenerating}
                  >
                    <RefreshCw
                      className={`mr-2 h-4 w-4 ${
                        isGenerating ? 'animate-spin' : ''
                      }`}
                    />
                    {isGenerating ? 'Generating PDF' : 'Generate PDF rendition'}
                  </Button>
                ) : null}
                {generateError ? (
                  <p className="mt-3 text-sm text-destructive">
                    {generateError}
                  </p>
                ) : null}
                {view.originalUrl ? (
                  <Button
                    type="button"
                    variant="outline"
                    className="mt-4"
                    disabled={openingSource}
                    onClick={() => void handleOpenSourceFile()}
                  >
                    <ExternalLink
                      className={`mr-2 h-4 w-4 ${
                        openingSource ? 'animate-spin' : ''
                      }`}
                    />
                    Open source file
                  </Button>
                ) : null}
              </div>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
