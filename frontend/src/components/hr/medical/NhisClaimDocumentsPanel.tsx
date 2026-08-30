'use client';

import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, FileWarning, Loader2, Paperclip, Trash2, Upload } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { nhisClaimService } from '@/services/hr/medical-claims.service';
import type { NHISClaimDocument } from '@/types/hr/medical';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const MAX_BYTES = 10 * 1024 * 1024;
const fmtSize = (b: number) =>
  b >= 1_048_576 ? `${(b / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(b / 1024))} KB`;

/**
 * The paperwork behind an NHIS claim — the attendance record, the prescription, the scheme's own
 * correspondence.
 *
 * ⚠ **This collection was unbuildable until the upload endpoint existed.** The only create route
 * REQUIRED a caller-supplied `FilePath` and stored it verbatim — the fifth instance of that sink
 * in the medical module — and there was no download route either, so a row was unreadable even
 * when the path was honest (D-14). Files now pass the malware scanner into private storage and
 * come back through a token-bearing download.
 *
 * ⚠ **NHIS shares the medical-claim document category deliberately.** It is the same artefact
 * about the same person, read by the same permission family, as an expense-claim receipt; a
 * separate category would split one retention rule in two for no difference in content.
 *
 * ⚠ **The delete is Admin-tier while the upload is Write-tier** — the same ladder as the rest of
 * the medical module, where every removal of a medical record needs `HR.Medical.Admin`.
 */
export function NhisClaimDocumentsPanel({
  claimId,
  canWrite,
  canDelete,
}: {
  claimId: string;
  canWrite: boolean;
  canDelete: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const fileInput = useRef<HTMLInputElement>(null);

  const queryKey = ['hr', 'nhis-claims', claimId, 'documents'];
  const { data: documents, isLoading } = useQuery({
    queryKey,
    queryFn: () => nhisClaimService.getDocuments(claimId),
    enabled: !!claimId,
  });

  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [description, setDescription] = useState('');
  const [pendingDelete, setPendingDelete] = useState<NHISClaimDocument | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);

  const reset = () => {
    setFile(null);
    setDescription('');
    if (fileInput.current) fileInput.current.value = '';
  };

  const fail = (title: string) => (e: any) =>
    toast({
      title,
      description: e?.message ?? e?.response?.data?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const upload = useMutation({
    mutationFn: () =>
      nhisClaimService.uploadDocument(claimId, file as File, description.trim() || null),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey });
      toast({ title: 'Document attached' });
      setOpen(false);
      reset();
    },
    onError: fail('Could not attach the document'),
  });

  const remove = useMutation({
    mutationFn: (doc: NHISClaimDocument) => nhisClaimService.removeDocument(doc.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey });
      toast({ title: 'Document removed' });
      setPendingDelete(null);
    },
    onError: fail('Could not remove the document'),
  });

  const download = async (doc: NHISClaimDocument) => {
    setDownloading(doc.id);
    try {
      await nhisClaimService.downloadDocument(doc.id, doc.fileName);
    } catch (e) {
      fail('Could not download the document')(e);
    } finally {
      setDownloading(null);
    }
  };

  const tooBig = !!file && file.size > MAX_BYTES;
  const rows = documents ?? [];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          Files are scanned and stored privately — they can only be read back through this screen.
        </p>
        {canWrite && (
          <Button size="sm" onClick={() => { reset(); setOpen(true); }}>
            <Upload className="mr-2 h-4 w-4" />
            Attach
          </Button>
        )}
      </div>

      {isLoading ? (
        <div className="flex justify-center py-10">
          <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
        </div>
      ) : rows.length === 0 ? (
        <EmptyState
          icon={Paperclip}
          title="No documents"
          description="Nothing supports this claim — no attendance record, prescription or scheme correspondence."
        />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>File</TableHead>
              <TableHead>Uploaded</TableHead>
              <TableHead className="w-24 text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((d) => {
              // A row with no DMS id predates the upload gate: its filePath names a file the
              // server never received, so there is nothing to stream.
              const legacy = !d.fileUploadRecordId && !d.documentRecordId;
              return (
                <TableRow key={d.id}>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <span className="font-medium">{d.fileName}</span>
                      {legacy && (
                        <Badge variant="secondary" className="gap-1">
                          <FileWarning className="h-3 w-3" />
                          No file
                        </Badge>
                      )}
                    </div>
                    {d.description && (
                      <div className="text-xs text-muted-foreground">{d.description}</div>
                    )}
                  </TableCell>
                  <TableCell>{fmtDate(d.uploadDate)}</TableCell>
                  <TableCell className="text-right">
                    <div className="flex justify-end gap-1">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => download(d)}
                        disabled={downloading === d.id || legacy}
                        title={
                          legacy
                            ? 'This row predates the upload gate and has no stored file.'
                            : undefined
                        }
                      >
                        {downloading === d.id ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Download className="h-4 w-4" />
                        )}
                        <span className="sr-only">Download</span>
                      </Button>
                      {canDelete && (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-red-600"
                          onClick={() => setPendingDelete(d)}
                        >
                          <Trash2 className="h-4 w-4" />
                          <span className="sr-only">Remove</span>
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      )}

      <Dialog open={open} onOpenChange={(o) => { setOpen(o); if (!o) reset(); }}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>Attach a document</DialogTitle>
            <DialogDescription>
              The file is scanned before it is stored and is only readable through this screen.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="nd-file">
                File<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Input
                id="nd-file"
                type="file"
                ref={fileInput}
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              />
              {file && (
                <p className={`text-xs ${tooBig ? 'text-red-500' : 'text-muted-foreground'}`}>
                  {fmtSize(file.size)}
                  {tooBig && ' — over the 10 MB limit'}
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="nd-description">Description</Label>
              <Input
                id="nd-description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Optional — what this document is"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={upload.isPending}>
              Cancel
            </Button>
            <Button onClick={() => upload.mutate()} disabled={!file || tooBig || upload.isPending}>
              {upload.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Attach
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(o) => !o && setPendingDelete(null)}
        title="Remove this document?"
        description={`"${pendingDelete?.fileName ?? ''}" will be removed from the claim.`}
        confirmText="Remove"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={async () => {
          if (pendingDelete) await remove.mutateAsync(pendingDelete);
        }}
      />
    </div>
  );
}
