'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Loader2, Paperclip, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { DocumentUploadField } from '@/components/hr/common/DocumentUploadField';
import { useToast } from '@/hooks/use-toast';
import { apiService } from '@/services/api.service';
import { hrDocumentService } from '@/services/hr/hr-document.service';

export interface PerformanceAttachment {
  id: string;
  fileName: string;
  description?: string | null;
  uploadDate: string;
  fileSizeBytes?: number | null;
  uploadedByName?: string | null;
}

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtSize = (v?: number | null) =>
  v == null ? '—' : v < 1024 ? `${v} B` : v < 1048576 ? `${Math.round(v / 1024)} KB` : `${(v / 1048576).toFixed(1)} MB`;

/**
 * Evidence attached to an appraisal or a check-in.
 *
 * ⚠ **Both endpoints were built and never called.** The appraisal upload in particular was
 * purpose-built for the controlled gate — `IFormFile` in, `HrAttachmentUpload.ExecuteAsync`, a
 * token-bearing download — and nothing in the product ever reached it, so an appraisal's evidence
 * could not be attached at all. Check-ins were the same: goals and review events wire an attachment
 * panel and check-ins did not.
 *
 * ⚠ **One panel, two owners.** `CheckIns/{id}/attachments` and
 * `PerformanceAppraisals/{id}/attachments` are the same four routes with a different prefix, so a
 * second component would only be a chance for the two to drift.
 *
 * ⚠ **`filePath` is never a link.** The files sit outside the web root and the download needs the
 * bearer token, so an `<a href>` cannot work — everything goes through `hrDocumentService`.
 */
export function PerformanceAttachmentsPanel({
  basePath,
  ownerId,
  canUpload = true,
  canDelete = true,
  helpText = 'Evidence supporting this record. Scanned on upload; max 10 MB.',
  listPath,
  uploadPath,
  downloadPath,
  deletePath,
  uploadFields,
}: {
  /** `/CheckIns` or `/PerformanceAppraisals` — no trailing slash. */
  basePath: string;
  ownerId: string;
  canUpload?: boolean;
  canDelete?: boolean;
  helpText?: string;
  /**
   * ⚠ Overrides for a family whose routes are not `{base}/{owner}/attachments/{id}`.
   *
   * The awards routes are deliberately asymmetric — an attachment is CREATED under its award or
   * nomination and then addressed on its own (`Awards/attachments/{id}`, and
   * `Awards/nomination-attachments/{id}` for the other family) — so the panel takes the four paths
   * rather than assuming one shape. Two near-identical components would only drift.
   */
  listPath?: string;
  uploadPath?: string;
  downloadPath?: (attachmentId: string) => string;
  deletePath?: (attachmentId: string) => string;
  /** Extra form fields the upload endpoint expects beside the file, e.g. an attachment type. */
  uploadFields?: Record<string, string | number | boolean | undefined | null>;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const list = listPath ?? `${basePath}/${ownerId}/attachments`;
  const upload = uploadPath ?? list;
  const toDownload = downloadPath ?? ((id: string) => `${basePath}/${ownerId}/attachments/${id}/download`);
  const toDelete = deletePath ?? ((id: string) => `${basePath}/${ownerId}/attachments/${id}`);

  const queryKey = ['hr', 'attachments', list];

  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () => apiService.get<PerformanceAttachment[]>(list),
    enabled: Boolean(ownerId),
  });

  const remove = useMutation({
    mutationFn: (attachmentId: string) => apiService.delete<void>(toDelete(attachmentId)),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey });
      toast({ title: 'Attachment removed' });
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'It could not be removed',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const download = async (attachmentId: string, fileName: string) => {
    try {
      await hrDocumentService.download(toDownload(attachmentId), fileName);
    } catch (e: any) {
      toast({
        variant: 'destructive',
        title: 'It could not be downloaded',
        description: e?.body?.detail ?? e?.message,
      });
    }
  };

  const rows = data ?? [];

  return (
    <div className="space-y-4">
      {canUpload && (
        <Card>
          <CardContent className="pt-6">
            <DocumentUploadField
              label="Attach evidence"
              endpoint={upload}
              fields={uploadFields}
              accept=".pdf,.png,.jpg,.jpeg,.doc,.docx,.xls,.xlsx"
              helpText={helpText}
              onUploaded={() => queryClient.invalidateQueries({ queryKey })}
            />
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Paperclip}
              title="No attachments"
              description="Evidence attached here is visible to anyone who can see this record."
            />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>File</TableHead>
                    <TableHead>Description</TableHead>
                    <TableHead>Uploaded</TableHead>
                    <TableHead className="text-right">Size</TableHead>
                    <TableHead />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell className="font-medium">{a.fileName}</TableCell>
                      <TableCell className="text-muted-foreground">{a.description ?? '—'}</TableCell>
                      <TableCell>
                        <div>{fmtDate(a.uploadDate)}</div>
                        {a.uploadedByName && (
                          <div className="text-xs text-muted-foreground">{a.uploadedByName}</div>
                        )}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{fmtSize(a.fileSizeBytes)}</TableCell>
                      <TableCell className="text-right">
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => download(a.id, a.fileName)}
                          title="Download"
                        >
                          <Download className="h-4 w-4" />
                        </Button>
                        {canDelete && (
                          <Button
                            size="sm"
                            variant="ghost"
                            disabled={remove.isPending}
                            onClick={() => remove.mutate(a.id)}
                            title="Remove"
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
