'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import { Download, ExternalLink, FileWarning, RefreshCw } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { documentManagementService } from '@/services/document-management.service';

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
}

interface CentralDocumentViewerDialogProps {
  file: CentralDocumentViewerFile | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  enableAnnotations?: boolean;
  onGenerateRendition?: (
    file: CentralDocumentViewerFile
  ) => Promise<CentralDocumentViewerFile | null>;
  onDownload?: (
    file: CentralDocumentViewerFile,
    format: 'pdf' | 'word'
  ) => Promise<void>;
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

function resolvePdfView(file: CentralDocumentViewerFile | null) {
  if (!file) {
    return { url: null, originalUrl: null, status: 'none' as const };
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

export function CentralDocumentViewerDialog({
  file,
  open,
  onOpenChange,
  enableAnnotations = true,
  onGenerateRendition,
  onDownload,
}: CentralDocumentViewerDialogProps) {
  void enableAnnotations;
  const annotationsEnabled = false;
  const [localFile, setLocalFile] =
    React.useState<CentralDocumentViewerFile | null>(file);
  const [previewObjectUrl, setPreviewObjectUrl] = React.useState<string | null>(
    null
  );
  const [isLoadingPreview, setIsLoadingPreview] = React.useState(false);
  const [isGenerating, setIsGenerating] = React.useState(false);
  const [downloadingFormat, setDownloadingFormat] = React.useState<
    'pdf' | 'word' | null
  >(null);
  const [generateError, setGenerateError] = React.useState<string | null>(null);
  const [downloadError, setDownloadError] = React.useState<string | null>(null);

  React.useEffect(() => {
    setLocalFile(file);
    setGenerateError(null);
    setDownloadError(null);
    setDownloadingFormat(null);
  }, [file]);

  const view = resolvePdfView(localFile);
  const viewerUrl = previewObjectUrl || view.url;
  const title = localFile?.title || 'Document preview';
  const canGenerateRendition =
    view.status === 'conversion-required' &&
    Boolean(localFile?.documentRecordId && localFile?.versionId) &&
    Boolean(onGenerateRendition);
  const canDownloadVersion =
    Boolean(localFile?.documentRecordId && localFile?.versionId) &&
    Boolean(onDownload);

  React.useEffect(() => {
    let cancelled = false;
    let objectUrl: string | null = null;

    setPreviewObjectUrl(null);

    if (!open || !localFile?.documentRecordId) {
      setIsLoadingPreview(false);
      return () => undefined;
    }

    const shouldFetchSecurePreview =
      Boolean(localFile.versionId) ||
      view.url?.startsWith('/api/document-management/');

    if (!shouldFetchSecurePreview) {
      setIsLoadingPreview(false);
      return () => undefined;
    }

    setIsLoadingPreview(true);
    const loadPreview = async () => {
      try {
        const blob = localFile.versionId
          ? await documentManagementService.downloadVersionFile(
              localFile.documentRecordId!,
              localFile.versionId,
              'pdf'
            )
          : await documentManagementService.downloadRecordContent(
              localFile.documentRecordId!
            );

        if (cancelled) return;

        objectUrl = URL.createObjectURL(blob);
        setPreviewObjectUrl(objectUrl);
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
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [
    open,
    localFile?.documentRecordId,
    localFile?.versionId,
    localFile?.repositoryPath,
    localFile?.renditionPath,
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

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex h-[92vh] max-h-[92vh] w-[96vw] max-w-7xl flex-col overflow-hidden p-0">
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
          {downloadError ? (
            <p className="mt-2 text-sm text-destructive">{downloadError}</p>
          ) : null}
        </DialogHeader>

        <div className="min-h-0 flex-1 overflow-hidden bg-background p-4">
          {viewerUrl ? (
            <CentralDocumentPdfViewer
              fileUrl={viewerUrl}
              fileName={localFile?.fileName || title}
              enableAnnotations={annotationsEnabled}
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
                {view.originalUrl &&
                !view.originalUrl.startsWith('/api/document-management/') ? (
                  <Button asChild variant="outline" className="mt-4">
                    <a
                      href={view.originalUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      <ExternalLink className="mr-2 h-4 w-4" />
                      Open source file
                    </a>
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
