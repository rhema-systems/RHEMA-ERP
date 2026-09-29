'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Loader2, Paperclip, Pencil, Trash2 } from 'lucide-react';
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
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { DocumentUploadField } from '@/components/hr/common/DocumentUploadField';
import { useToast } from '@/hooks/use-toast';
import { apiService } from '@/services/api.service';
import { hrDocumentService } from '@/services/hr/hr-document.service';

export interface PerformanceAttachment {
  id: string;
  fileName: string;
  description?: string | null;
  /**
   * Present on the awards families. ⚠ The panel's type used to omit these although the API returns
   * them, so a caller had no way to show or correct what an attachment IS — which mattered, because
   * the nomination screen uploads everything as `Citation`.
   */
  attachmentType?: string | null;
  attachmentTypeName?: string | null;
  uploadDate: string;
  fileSizeBytes?: number | null;
  /** The employee who attached it — what {@link canDeleteItem} rules usually compare against. */
  uploadedById?: string | null;
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
  canDeleteItem,
  helpText = 'Evidence supporting this record. Scanned on upload; max 10 MB.',
  listPath,
  uploadPath,
  downloadPath,
  deletePath,
  editPath,
  attachmentTypes,
  uploadFields,
}: {
  /** `/CheckIns` or `/PerformanceAppraisals` — no trailing slash. */
  basePath: string;
  ownerId: string;
  canUpload?: boolean;
  canDelete?: boolean;
  /**
   * A per-row rule on top of {@link canDelete}. The appraisal and check-in families remove a file
   * only for its uploader or HR, and only before the record is complete (performance closure
   * P9) — the server refuses anyone else, so the button should not be offered to them.
   */
  canDeleteItem?: (attachment: PerformanceAttachment) => boolean;
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
  /**
   * Enables the correction dialog. Only supply it for a family whose API has an update route —
   * today that is award nomination attachments alone (`PUT Awards/nomination-attachments/{id}`).
   * Without it the panel renders exactly as before.
   */
  editPath?: (attachmentId: string) => string;
  /** The values the edit dialog offers for the type. Required alongside `editPath`. */
  attachmentTypes?: readonly string[];
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

  const [editing, setEditing] = useState<PerformanceAttachment | null>(null);
  const [editType, setEditType] = useState('');
  const [editDescription, setEditDescription] = useState('');

  /**
   * ⚠ Corrects only what an attachment IS — never where it lives. `UpdateAwardNominationAttachmentDto`
   * carries the type and the description and nothing else; the file's own name, path and size are
   * not on it, and a body carrying them is ignored. That is deliberate and proven: D-39 took
   * `FileName`/`FilePath` off the create DTOs because an "attachment" used to be a string somebody
   * typed, and `hr-awards/probe-lane3-awards.mjs` sends `C:\Windows\System32\config\SAM`
   * through this very route to assert the stored file is untouched.
   */
  const edit = useMutation({
    mutationFn: () => {
      if (!editing || !editPath) throw new Error('No attachment selected.');
      return apiService.put<void>(editPath(editing.id), {
        id: editing.id,
        attachmentType: editType,
        description: editDescription.trim() || null,
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey });
      setEditing(null);
      toast({ title: 'Attachment updated' });
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'It could not be updated',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const openEdit = (a: PerformanceAttachment) => {
    setEditType(a.attachmentType ?? attachmentTypes?.[0] ?? '');
    setEditDescription(a.description ?? '');
    setEditing(a);
  };

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
                    {attachmentTypes && <TableHead>Type</TableHead>}
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
                      {attachmentTypes && (
                        <TableCell className="text-muted-foreground">
                          {a.attachmentTypeName ?? a.attachmentType ?? '—'}
                        </TableCell>
                      )}
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
                        {editPath && (
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() => openEdit(a)}
                            title="Correct what this is"
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                        )}
                        {canDelete && (canDeleteItem?.(a) ?? true) && (
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

      <Dialog open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Correct {editing?.fileName}</DialogTitle>
            <DialogDescription>
              Changes what this attachment is recorded as. The file itself is untouched — to replace
              it, upload the new one and remove this.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="att-type">Type</Label>
              <Select value={editType} onValueChange={setEditType}>
                <SelectTrigger id="att-type"><SelectValue placeholder="Choose a type" /></SelectTrigger>
                <SelectContent>
                  {(attachmentTypes ?? []).map((t) => (
                    <SelectItem key={t} value={t}>
                      {t.replace(/([a-z])([A-Z])/g, '$1 $2')}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="att-description">Description</Label>
              <Textarea
                id="att-description"
                rows={3}
                value={editDescription}
                onChange={(e) => setEditDescription(e.target.value)}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)}>Cancel</Button>
            <Button
              onClick={() => edit.mutate()}
              disabled={edit.isPending || editType === ''}
            >
              {edit.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
