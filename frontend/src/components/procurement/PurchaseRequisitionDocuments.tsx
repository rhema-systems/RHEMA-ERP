'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertCircle, Download, Eye, FileText, Loader2, Paperclip, Trash2, Upload } from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  ProcurementManagedDocument,
  procurementDocumentManagementService,
} from '@/services/procurement-document-management.service';

export const REQUISITION_DOCUMENT_CLASSIFICATIONS = [
  'Specification',
  'Scope of work',
  'Drawing',
  'Cost estimate',
  'Budget support',
  'Justification',
  'Supporting document',
  'Other',
] as const;

export interface PendingPurchaseRequisitionDocument {
  clientId: string;
  file: File;
  classification: string;
  title: string;
}

export interface PendingDocumentUploadResult {
  uploaded: number;
  failed: PendingPurchaseRequisitionDocument[];
  errorMessages: string[];
}

type RequisitionDocumentUploader = (
  requisitionId: string,
  classification: string,
  file: File,
  title?: string
) => Promise<unknown>;

export async function uploadPendingPurchaseRequisitionDocuments(
  requisitionId: string,
  documents: PendingPurchaseRequisitionDocument[],
  upload: RequisitionDocumentUploader = (id, classification, file, title) =>
    procurementDocumentManagementService.upload('Requisition', id, classification, file, title)
): Promise<PendingDocumentUploadResult> {
  const failed: PendingPurchaseRequisitionDocument[] = [];
  const errorMessages: string[] = [];
  let uploaded = 0;

  for (const document of documents) {
    try {
      await upload(
        requisitionId,
        document.classification,
        document.file,
        document.title
      );
      uploaded += 1;
    } catch (error) {
      failed.push(document);
      errorMessages.push(getPurchaseRequisitionDocumentErrorMessage(error, `Could not upload ${document.file.name}.`));
    }
  }

  return { uploaded, failed, errorMessages };
}

interface PurchaseRequisitionDocumentsProps {
  requisitionId?: string;
  requisitionStatus?: string;
  editable?: boolean;
  pendingDocuments?: PendingPurchaseRequisitionDocument[];
  onPendingDocumentsChange?: (documents: PendingPurchaseRequisitionDocument[]) => void;
}

export function PurchaseRequisitionDocuments({
  requisitionId,
  requisitionStatus = 'Draft',
  editable = true,
  pendingDocuments = [],
  onPendingDocumentsChange,
}: PurchaseRequisitionDocumentsProps) {
  const [documents, setDocuments] = useState<ProcurementManagedDocument[]>([]);
  const [classifications, setClassifications] = useState<string[]>([
    ...REQUISITION_DOCUMENT_CLASSIFICATIONS,
  ]);
  const [classification, setClassification] = useState('Supporting document');
  const [title, setTitle] = useState('');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [inputKey, setInputKey] = useState(0);
  const [loading, setLoading] = useState(Boolean(requisitionId));
  const [uploading, setUploading] = useState(false);
  const [removing, setRemoving] = useState(false);
  const [removeTarget, setRemoveTarget] = useState<ProcurementManagedDocument | null>(null);
  const [error, setError] = useState<string | null>(null);

  const canChange = editable && requisitionStatus.toLowerCase() === 'draft';

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const catalogue = await procurementDocumentManagementService.catalogue();
      const family = catalogue.find((item) => item.code === 'Requisition');
      if (family?.classifications.length) {
        setClassifications(family.classifications);
        setClassification((current) =>
          family.classifications.includes(current) ? current : family.classifications[0]
        );
      }
      if (requisitionId) {
        setDocuments(
          await procurementDocumentManagementService.records('Requisition', requisitionId)
        );
      }
    } catch (loadError) {
      setError(getPurchaseRequisitionDocumentErrorMessage(loadError, 'Supporting documents could not be loaded.'));
    } finally {
      setLoading(false);
    }
  }, [requisitionId]);

  useEffect(() => {
    void load();
  }, [load]);

  const managedDocuments = useMemo(
    () => [...documents].sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc)),
    [documents]
  );

  const resetSelection = () => {
    setSelectedFile(null);
    setTitle('');
    setInputKey((value) => value + 1);
  };

  const addOrUpload = async () => {
    if (!selectedFile) {
      setError('Select a supporting document to continue.');
      return;
    }

    if (!requisitionId) {
      const queued: PendingPurchaseRequisitionDocument = {
        clientId: globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${selectedFile.name}`,
        file: selectedFile,
        classification,
        title: title.trim() || selectedFile.name,
      };
      onPendingDocumentsChange?.([...pendingDocuments, queued]);
      resetSelection();
      setError(null);
      return;
    }

    try {
      setUploading(true);
      setError(null);
      await procurementDocumentManagementService.upload(
        'Requisition',
        requisitionId,
        classification,
        selectedFile,
        title
      );
      resetSelection();
      await load();
      toast.success('Supporting document uploaded');
    } catch (uploadError) {
      const message = getPurchaseRequisitionDocumentErrorMessage(uploadError, 'Supporting document upload failed.');
      setError(message);
      toast.error(message);
    } finally {
      setUploading(false);
    }
  };

  const viewDocument = async (document: ProcurementManagedDocument) => {
    try {
      setError(null);
      const blob = await procurementDocumentManagementService.download(
        document.documentRecordId,
        document.documentVersionId
      );
      const url = URL.createObjectURL(blob);
      const link = window.document.createElement('a');
      link.href = url;
      link.target = '_blank';
      link.rel = 'noopener noreferrer';
      link.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 30_000);
    } catch (viewError) {
      const message = getPurchaseRequisitionDocumentErrorMessage(viewError, 'Supporting document could not be opened.');
      setError(message);
      toast.error(message);
    }
  };

  const confirmRemove = async () => {
    if (!removeTarget) return false;
    try {
      setRemoving(true);
      setError(null);
      await procurementDocumentManagementService.removeRequisitionDocument(
        removeTarget.documentRecordId
      );
      setDocuments((current) =>
        current.filter((item) => item.documentRecordId !== removeTarget.documentRecordId)
      );
      setRemoveTarget(null);
      toast.success('Supporting document removed');
      return true;
    } catch (removeError) {
      const message = getPurchaseRequisitionDocumentErrorMessage(removeError, 'Supporting document could not be removed.');
      setError(message);
      toast.error(message);
      return false;
    } finally {
      setRemoving(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Paperclip className="h-5 w-5" />
          Supporting documents
        </CardTitle>
        <CardDescription>
          Attach specifications, drawings, estimates or other records that support this requisition.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {error && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {canChange && (
          <div className="grid gap-3 rounded-lg border p-4 md:grid-cols-[minmax(0,1fr)_220px_minmax(0,1fr)_auto] md:items-end">
            <div className="space-y-2">
              <Label htmlFor="requisition-supporting-file">Document</Label>
              <Input
                key={inputKey}
                id="requisition-supporting-file"
                type="file"
                onChange={(event) => setSelectedFile(event.target.files?.[0] ?? null)}
                disabled={uploading}
              />
            </div>
            <div className="space-y-2">
              <Label>Classification</Label>
              <Select value={classification} onValueChange={setClassification} disabled={uploading}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {classifications.map((option) => (
                    <SelectItem key={option} value={option}>{option}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="requisition-document-title">Title (optional)</Label>
              <Input
                id="requisition-document-title"
                value={title}
                maxLength={250}
                onChange={(event) => setTitle(event.target.value)}
                placeholder={selectedFile?.name ?? 'Document title'}
                disabled={uploading}
              />
            </div>
            <Button type="button" onClick={addOrUpload} disabled={!selectedFile || uploading}>
              {uploading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Upload className="mr-2 h-4 w-4" />}
              {requisitionId ? 'Upload' : 'Add'}
            </Button>
          </div>
        )}

        {!requisitionId && pendingDocuments.length > 0 && (
          <div className="space-y-2">
            {pendingDocuments.map((document) => (
              <div key={document.clientId} className="flex items-center justify-between gap-3 rounded-lg border p-3">
                <div className="min-w-0">
                  <div className="truncate font-medium">{document.title}</div>
                  <div className="text-sm text-muted-foreground">{document.classification} · {document.file.name}</div>
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={`Remove ${document.title}`}
                  onClick={() => onPendingDocumentsChange?.(
                    pendingDocuments.filter((item) => item.clientId !== document.clientId)
                  )}
                >
                  <Trash2 className="h-4 w-4 text-destructive" />
                </Button>
              </div>
            ))}
          </div>
        )}

        {requisitionId && loading ? (
          <div className="flex items-center gap-2 py-4 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Loading supporting documents…
          </div>
        ) : requisitionId && managedDocuments.length === 0 ? (
          <div className="rounded-lg border border-dashed p-6 text-center text-sm text-muted-foreground">
            No supporting documents attached.
          </div>
        ) : requisitionId ? (
          <div className="space-y-2">
            {managedDocuments.map((document) => (
              <div key={document.documentRecordId} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3">
                <div className="flex min-w-0 items-start gap-3">
                  <FileText className="mt-0.5 h-5 w-5 shrink-0 text-muted-foreground" />
                  <div className="min-w-0">
                    <div className="truncate font-medium">{document.title}</div>
                    <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                      <Badge variant="outline">{document.classification}</Badge>
                      <span>{document.versionNumber}</span>
                      <span>{new Date(document.createdAtUtc).toLocaleString()}</span>
                      <span>{document.malwareStatus}</span>
                    </div>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <Button type="button" variant="outline" size="sm" onClick={() => void viewDocument(document)}>
                    {document.malwareStatus.toLowerCase() === 'clean' ? <Eye className="mr-2 h-4 w-4" /> : <Download className="mr-2 h-4 w-4" />}
                    View
                  </Button>
                  {canChange && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      aria-label={`Remove ${document.title}`}
                      onClick={() => setRemoveTarget(document)}
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  )}
                </div>
              </div>
            ))}
          </div>
        ) : null}
      </CardContent>

      <ConfirmationDialog
        open={Boolean(removeTarget)}
        onOpenChange={(open) => { if (!open && !removing) setRemoveTarget(null); }}
        title="Remove supporting document?"
        description="This removes the document from the draft requisition and records the action in the audit log."
        confirmText="Remove document"
        variant="destructive"
        isLoading={removing}
        onConfirm={confirmRemove}
      />
    </Card>
  );
}

export function getPurchaseRequisitionDocumentErrorMessage(error: unknown, fallback: string): string {
  if (error && typeof error === 'object') {
    const candidate = error as {
      detail?: unknown;
      message?: unknown;
      code?: unknown;
      response?: { detail?: unknown; code?: unknown; extensions?: { code?: unknown } };
    };
    const detail = typeof candidate.response?.detail === 'string'
      ? candidate.response.detail
      : typeof candidate.detail === 'string'
        ? candidate.detail
      : typeof candidate.message === 'string'
        ? candidate.message
        : fallback;
    const code = candidate.response?.code ?? candidate.response?.extensions?.code ?? candidate.code;
    return typeof code === 'string' && code && !detail.includes(code)
      ? `${detail} (${code})`
      : detail;
  }
  return fallback;
}
