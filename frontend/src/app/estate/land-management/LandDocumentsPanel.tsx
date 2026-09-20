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
import { Checkbox } from '@/components/ui/checkbox';
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
  const [bulkPublishing, setBulkPublishing] = React.useState(false);
  const [selectedDocumentIds, setSelectedDocumentIds] = React.useState<
    Set<string>
  >(new Set());
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

  React.useEffect(() => {
    setSelectedDocumentIds((current) => {
      const available = new Set(documents.map((document) => document.id));
      const next = new Set(
        [...current].filter((documentId) => available.has(documentId))
      );
      return next.size === current.size ? current : next;
    });
  }, [documents]);

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
      const result = await fileDocumentInCentralDms(document.id);
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
      toast.success(`Filed in Central DMS as ${result.documentReference}.`);
    } catch (error: any) {
      toast.error(
        error?.message || 'Unable to file document in Central DMS.'
      );
    } finally {
      setPublishingId(null);
    }
  };

  const fileDocumentInCentralDms = (documentId: string) =>
    estateLandManagementService.publishDocumentToCentralDms(
      assetId,
      documentId
    );

  const toggleDocumentSelection = (documentId: string, checked: boolean) => {
    setSelectedDocumentIds((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(documentId);
      } else {
        next.delete(documentId);
      }
      return next;
    });
  };

  const selectableDocumentIds = documents.map((document) => document.id);
  const allSelected =
    selectableDocumentIds.length > 0 &&
    selectableDocumentIds.every((documentId) =>
      selectedDocumentIds.has(documentId)
    );
  const selectedDocuments = documents.filter((document) =>
    selectedDocumentIds.has(document.id)
  );

  const toggleAllDocuments = (checked: boolean) => {
    setSelectedDocumentIds(
      checked ? new Set(selectableDocumentIds) : new Set()
    );
  };

  const fileSelectedInCentralDms = async () => {
    if (selectedDocuments.length === 0) {
      toast.error('Select at least one document to file in DMS.');
      return;
    }

    try {
      setBulkPublishing(true);
      const updates = new Map<string, { id: string; documentReference: string; publishedToCentralDmsAt?: string | null }>();
      for (const document of selectedDocuments) {
        const result = await fileDocumentInCentralDms(document.id);
        updates.set(document.id, result);
      }

      setDocuments((current) =>
        current.map((item) => {
          const result = updates.get(item.id);
          return result
            ? {
                ...item,
                centralDocumentRecordId: result.id,
                centralDocumentReference: result.documentReference,
                publishedToCentralDmsAt:
                  result.publishedToCentralDmsAt || new Date().toISOString(),
              }
            : item;
        })
      );
      setSelectedDocumentIds(new Set());
      toast.success(
        `Filed ${updates.size} document${updates.size === 1 ? '' : 's'} in Central DMS.`
      );
    } catch (error: any) {
      toast.error(error?.message || 'Unable to file selected documents in Central DMS.');
    } finally {
      setBulkPublishing(false);
    }
  };

  return (
    <div className="space-y-3 rounded-md border bg-background p-4">
      <div>
        <p className="text-sm font-semibold">Land Documents</p>
        <p className="text-xs text-muted-foreground">
          Files are stored in the Estate / Facility repository and can be
          filed in Central DMS.
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

      {documents.length > 0 ? (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border bg-muted/30 px-3 py-2 text-sm">
          <label className="flex items-center gap-2">
            <Checkbox
              checked={allSelected}
              disabled={bulkPublishing}
              onCheckedChange={(checked) =>
                toggleAllDocuments(checked === true)
              }
            />
            <span>Select all documents</span>
          </label>
          <Button
            size="sm"
            disabled={bulkPublishing || selectedDocuments.length === 0}
            onClick={() => void fileSelectedInCentralDms()}
          >
            {bulkPublishing ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Send className="mr-2 h-4 w-4" />
            )}
            File selected in DMS
          </Button>
        </div>
      ) : null}

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
                <Checkbox
                  checked={selectedDocumentIds.has(document.id)}
                  disabled={bulkPublishing}
                  onCheckedChange={(checked) =>
                    toggleDocumentSelection(document.id, checked === true)
                  }
                />
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
                  disabled={bulkPublishing || publishingId === document.id}
                  onClick={() => void publishToCentralDms(document)}
                >
                  {publishingId === document.id ? (
                    <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                  ) : (
                    <Send className="mr-1 h-4 w-4" />
                  )}
                  {document.centralDocumentReference
                    ? 'Sync DMS'
                    : 'File in DMS'}
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
