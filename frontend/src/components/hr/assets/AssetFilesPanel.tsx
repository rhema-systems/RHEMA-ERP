'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, FileText, ImageIcon, Loader2, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const fmtSize = (bytes?: number | null) => {
  if (bytes === null || bytes === undefined) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

/**
 * The documents and photographs filed against an asset — area 16 slice 12b.
 *
 * ⚠ **Both uploads go through the controlled gate**: the file is scanned and registered in the
 * central DMS before any row is written, and the row is rolled back if that write then fails.
 * Until this slice the create endpoints took a `fileName` and a `filePath` as JSON — the caller
 * named a path, the server wrote the string down, and no file existed anywhere. The list rendered
 * beautifully and pointed at nothing.
 *
 * ⚠ **`filePath` is not a URL and must never be rendered as a link.** The files live outside the
 * web root; the download endpoints are the only way to the bytes, and they need the bearer token,
 * which an `<a href>` cannot attach. Everything here goes through `hrDocumentService`, which
 * fetches as a blob and hands the browser an object URL it then revokes.
 */
export function AssetFilesPanel({ assetId }: { assetId: string }) {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [busyId, setBusyId] = useState<string | null>(null);

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'assets', 'files', assetId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'assets', 'detail', assetId] });
  };

  const { data: attachments = [], isLoading: loadingDocs } = useQuery({
    queryKey: ['hr', 'assets', 'files', assetId, 'attachments'],
    queryFn: () => assetRegisterService.getAttachments(assetId),
  });

  const { data: images = [], isLoading: loadingImages } = useQuery({
    queryKey: ['hr', 'assets', 'files', assetId, 'images'],
    queryFn: () => assetRegisterService.getImages(assetId),
  });

  const open = async (fn: () => Promise<void>, id: string) => {
    setBusyId(id);
    try {
      await fn();
    } catch (e) {
      toast({
        title: 'Could not open the file',
        description: (e as Error).message,
        variant: 'destructive',
      });
    } finally {
      setBusyId(null);
    }
  };

  const removeAttachment = useMutation({
    mutationFn: (id: string) => assetRegisterService.deleteAttachment(id),
    onSuccess: () => { invalidate(); toast({ title: 'Document removed' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not remove it', description: e.message, variant: 'destructive' }),
  });

  const removeImage = useMutation({
    mutationFn: (id: string) => assetRegisterService.deleteImage(id),
    onSuccess: () => { invalidate(); toast({ title: 'Photograph removed' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not remove it', description: e.message, variant: 'destructive' }),
  });

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <FileText className="h-4 w-4" /> Documents
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <DocumentUploadField
            label="Add a document"
            endpoint={`/Assets/${assetId}/attachments`}
            accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg"
            maxSizeMb={25}
            helpText="The invoice, the warranty certificate, the manual. Scanned before it is stored."
            onUploaded={() => { invalidate(); toast({ title: 'Document filed' }); }}
          />

          {loadingDocs ? (
            <div className="flex justify-center p-6">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : attachments.length === 0 ? (
            <EmptyState
              icon={FileText}
              title="No documents"
              description="Nothing has been filed against this asset yet."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>File</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead>Size</TableHead>
                  <TableHead>Filed</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {attachments.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell className="font-medium">{a.fileName}</TableCell>
                    <TableCell>{a.description ?? '—'}</TableCell>
                    <TableCell>{fmtSize(a.fileSizeBytes)}</TableCell>
                    <TableCell>{fmtDate(a.uploadDate)}</TableCell>
                    <TableCell className="text-right">
                      {/* ⚠ A row written before the gate has no stored file behind it. Saying so
                          beats a download button that answers 404. */}
                      {a.isStored ? (
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={busyId === a.id}
                          onClick={() => open(
                            () => assetRegisterService.downloadAttachment(a.id, a.fileName), a.id)}
                        >
                          {busyId === a.id
                            ? <Loader2 className="h-4 w-4 animate-spin" />
                            : <Download className="h-4 w-4" />}
                        </Button>
                      ) : (
                        <span className="mr-2 text-xs text-muted-foreground">No file stored</span>
                      )}
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={removeAttachment.isPending}
                        onClick={() => removeAttachment.mutate(a.id)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <ImageIcon className="h-4 w-4" /> Photographs
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <DocumentUploadField
            label="Add a photograph"
            endpoint={`/Assets/${assetId}/images`}
            fields={{ caption: '' }}
            accept="image/*"
            maxSizeMb={25}
            helpText="Its condition when issued or taken back. A photograph of damage is evidence in a charge, so caption it."
            onUploaded={() => { invalidate(); toast({ title: 'Photograph filed' }); }}
          />

          {loadingImages ? (
            <div className="flex justify-center p-6">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : images.length === 0 ? (
            <EmptyState
              icon={ImageIcon}
              title="No photographs"
              description="Nothing recorded about this asset's condition."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>File</TableHead>
                  <TableHead>Caption</TableHead>
                  <TableHead>Size</TableHead>
                  <TableHead>Taken</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {images.map((i) => (
                  <TableRow key={i.id}>
                    <TableCell className="font-medium">{i.fileName}</TableCell>
                    <TableCell>{i.caption ?? '—'}</TableCell>
                    <TableCell>{fmtSize(i.fileSizeBytes)}</TableCell>
                    <TableCell>{fmtDate(i.uploadDate)}</TableCell>
                    <TableCell className="text-right">
                      {i.isStored ? (
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={busyId === i.id}
                          onClick={() => open(() => assetRegisterService.openImage(i.id), i.id)}
                        >
                          {busyId === i.id
                            ? <Loader2 className="h-4 w-4 animate-spin" />
                            : <ImageIcon className="h-4 w-4" />}
                        </Button>
                      ) : (
                        <span className="mr-2 text-xs text-muted-foreground">No file stored</span>
                      )}
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={removeImage.isPending}
                        onClick={() => removeImage.mutate(i.id)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
