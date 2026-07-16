'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import {
  CheckCircle2,
  Download,
  Eye,
  FileText,
  Loader2,
  Send,
} from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  estateLandManagementService,
  type EstateManagedAssetDocument,
} from '@/services/estate-land-management.service';

const ProcedurePdfViewer = dynamic(
  () => import('@/components/procedures/ProcedurePdfViewer'),
  { ssr: false }
);

const documentTypes = [
  'Title Document',
  'Survey Plan',
  'Indenture',
  'Allocation Letter',
  'Search Report',
  'Site Plan',
  'Valuation Report',
  'Other',
];

export default function LandDocumentsPanel({ assetId }: { assetId: string }) {
  const [documents, setDocuments] = React.useState<
    EstateManagedAssetDocument[]
  >([]);
  const [documentType, setDocumentType] = React.useState('Other');
  const [uploading, setUploading] = React.useState(false);
  const [publishingId, setPublishingId] = React.useState<string | null>(null);
  const [preview, setPreview] = React.useState<{
    url: string;
    name: string;
  } | null>(null);

  const load = React.useCallback(async () => {
    try {
      setDocuments(await estateLandManagementService.getDocuments(assetId));
    } catch {
      toast.error('Unable to load land documents.');
    }
  }, [assetId]);

  React.useEffect(() => {
    void load();
  }, [load]);

  React.useEffect(
    () => () => {
      if (preview?.url) URL.revokeObjectURL(preview.url);
    },
    [preview?.url]
  );

  const openDocument = async (
    document: EstateManagedAssetDocument,
    view: boolean
  ) => {
    try {
      const blob = await estateLandManagementService.downloadDocument(
        assetId,
        document.id
      );
      const url = URL.createObjectURL(blob);

      if (view) {
        setPreview((current) => {
          if (current?.url) URL.revokeObjectURL(current.url);
          return { url, name: document.fileName };
        });
      } else {
        const link = window.document.createElement('a');
        link.href = url;
        link.download = document.fileName;
        window.document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
      }
    } catch (error: any) {
      toast.error(error?.message || 'Unable to open land document.');
    }
  };

  const uploadDocument = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.currentTarget.value = '';
    if (!file) return;

    try {
      setUploading(true);
      await estateLandManagementService.uploadDocument(
        assetId,
        file,
        documentType,
        file.name
      );
      await load();
      toast.success('Document uploaded.');
    } catch (error: any) {
      toast.error(error?.message || 'Unable to upload document.');
    } finally {
      setUploading(false);
    }
  };

  const publishToCentralDms = async (document: EstateManagedAssetDocument) => {
    try {
      setPublishingId(document.id);
      const result =
        await estateLandManagementService.publishDocumentToCentralDms(
          assetId,
          document.id
        );
      setDocuments((current) =>
        current.map((item) =>
          item.id === document.id
            ? {
                ...item,
                centralDocumentRecordId: result.id,
                centralDocumentReference: result.documentReference,
                publishedToCentralDmsAt:
                  result.publishedToCentralDmsAt || new Date().toISOString(),
              }
            : item
        )
      );
      toast.success(`Published to Central DMS as ${result.documentReference}.`);
    } catch (error: any) {
      toast.error(
        error?.message || 'Unable to publish document to Central DMS.'
      );
    } finally {
      setPublishingId(null);
    }
  };

  return (
    <div className="space-y-3 rounded-md border bg-background p-4">
      <div>
        <p className="text-sm font-semibold">Land Documents</p>
        <p className="text-xs text-muted-foreground">
          Files are stored in the Estate / Facility repository and can be
          published to Central DMS.
        </p>
      </div>

      <div className="grid gap-2 md:grid-cols-[220px_1fr]">
        <Select value={documentType} onValueChange={setDocumentType}>
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {documentTypes.map((type) => (
              <SelectItem key={type} value={type}>
                {type}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <div className="relative">
          <Input
            type="file"
            accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png"
            disabled={uploading}
            onChange={uploadDocument}
          />
          {uploading ? (
            <Loader2 className="absolute right-3 top-2.5 h-4 w-4 animate-spin" />
          ) : null}
        </div>
      </div>

      <div className="space-y-2">
        {documents.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No documents uploaded.
          </p>
        ) : (
          documents.map((document) => (
            <div
              key={document.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-md border px-3 py-2 text-sm"
            >
              <div className="flex min-w-0 items-center gap-2">
                <FileText className="h-4 w-4 shrink-0" />
                <div className="min-w-0">
                  <p className="truncate font-medium">
                    {document.documentName || document.fileName}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {document.documentType} / {document.uploadedBy || 'System'}
                  </p>
                  {document.centralDocumentReference ? (
                    <Badge variant="secondary" className="mt-1 w-fit">
                      <CheckCircle2 className="mr-1 h-3 w-3" />
                      Central DMS {document.centralDocumentReference}
                    </Badge>
                  ) : null}
                </div>
              </div>

              <div className="flex flex-wrap gap-1">
                {document.fileName.toLowerCase().endsWith('.pdf') ? (
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => void openDocument(document, true)}
                  >
                    <Eye className="mr-1 h-4 w-4" />
                    View
                  </Button>
                ) : null}
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={() => void openDocument(document, false)}
                >
                  <Download className="mr-1 h-4 w-4" />
                  Download
                </Button>
                <Button
                  size="sm"
                  variant={
                    document.centralDocumentReference ? 'outline' : 'default'
                  }
                  disabled={publishingId === document.id}
                  onClick={() => void publishToCentralDms(document)}
                >
                  {publishingId === document.id ? (
                    <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                  ) : (
                    <Send className="mr-1 h-4 w-4" />
                  )}
                  {document.centralDocumentReference
                    ? 'Sync DMS'
                    : 'Publish DMS'}
                </Button>
              </div>
            </div>
          ))
        )}
      </div>

      <Dialog
        open={Boolean(preview)}
        onOpenChange={(open) => !open && setPreview(null)}
      >
        <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{preview?.name}</DialogTitle>
          </DialogHeader>
          {preview ? (
            <ProcedurePdfViewer fileUrl={preview.url} fileName={preview.name} />
          ) : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}
