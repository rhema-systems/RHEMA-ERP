'use client';

import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Paperclip, Trash2, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { formatDateTime } from '@/lib/hr/attendance-format';

/**
 * The only fields this panel renders.
 *
 * Declared structurally rather than tying the component to one area's DTO: medical examination
 * documents carry no uploader name, and casting them into an appraisal attachment to satisfy the
 * old signature would have hidden exactly the kind of shape mismatch types exist to catch.
 */
export interface AttachmentLike {
  id: string;
  fileName: string;
  description?: string | null;
  uploadDate: string;
  /** Absent on areas that record the uploader as a user rather than an employee. */
  uploadedByName?: string | null;
}

interface AttachmentsPanelProps<TAttachment extends AttachmentLike> {
  title?: string;
  /** Must be stable and unique per parent record — it keys the cache. */
  queryKey: readonly unknown[];
  list: () => Promise<TAttachment[]>;
  upload: (file: File, description: string | null) => Promise<TAttachment>;
  download: (attachment: TAttachment) => Promise<void>;
  remove?: (attachmentId: string) => Promise<void>;
  readOnly?: boolean;
  emptyDescription?: string;
  /** Rendered under the header — the place to say who can see these. */
  note?: string;
}

/**
 * Evidence attached to an HR performance record.
 *
 * Every one of these areas goes through the same controlled-upload gate (scan, then central-DMS
 * registration) and serves files only from an authorizing download endpoint — stored paths are not
 * URLs and there is nothing to link to. That is why download is a callback rather than an `href`.
 *
 * The parent supplies the four operations because each area's routes and entitlement differ; what
 * is shared is the shape of the interaction and the fact that a refused upload comes back with a
 * reason worth showing verbatim.
 */
export function AttachmentsPanel<TAttachment extends AttachmentLike>({
  title = 'Attachments',
  queryKey,
  list,
  upload,
  download,
  remove,
  readOnly = false,
  emptyDescription = 'Nothing attached yet.',
  note,
}: AttachmentsPanelProps<TAttachment>) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [description, setDescription] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  const { data, isLoading } = useQuery({ queryKey, queryFn: list });

  const refresh = () => queryClient.invalidateQueries({ queryKey });

  const doUpload = useMutation({
    mutationFn: () => {
      if (!file) throw new Error('No file selected');
      return upload(file, description.trim() || null);
    },
    onSuccess: () => {
      refresh();
      setOpen(false);
      setFile(null);
      setDescription('');
      if (fileInputRef.current) fileInputRef.current.value = '';
      toast({ title: 'File attached' });
    },
    // The gate answers { code, message } — the message is the actionable part.
    onError: (err: unknown) =>
      toast({
        variant: 'destructive',
        title: 'Upload refused',
        description: (err as Error)?.message ?? 'Please try again.',
      }),
  });

  const doRemove = useMutation({
    mutationFn: (attachmentId: string) => {
      if (!remove) throw new Error('Removal is not available here');
      return remove(attachmentId);
    },
    onSuccess: () => {
      refresh();
      toast({ title: 'Attachment removed' });
    },
    onError: (err: unknown) =>
      toast({
        variant: 'destructive',
        title: 'Could not remove the attachment',
        description: (err as Error)?.message ?? 'Please try again.',
      }),
  });

  return (
    <>
      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-4">
          <div className="space-y-1.5">
            <CardTitle className="text-base">{title}</CardTitle>
            {note && <p className="text-sm text-muted-foreground">{note}</p>}
          </div>
          {!readOnly && (
            <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
              <Upload className="mr-2 h-4 w-4" />
              Attach
            </Button>
          )}
        </CardHeader>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="space-y-2 p-4">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : (data ?? []).length === 0 ? (
            <EmptyState icon={Paperclip} title="No files" description={emptyDescription} />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>File</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead className="w-40">Uploaded by</TableHead>
                  <TableHead className="w-40">When</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {(data ?? []).map((a) => (
                  <TableRow key={a.id}>
                    <TableCell className="font-medium">{a.fileName}</TableCell>
                    <TableCell className="text-muted-foreground">{a.description ?? '—'}</TableCell>
                    <TableCell>{a.uploadedByName || '—'}</TableCell>
                    <TableCell>{formatDateTime(a.uploadDate)}</TableCell>
                    <TableCell>
                      <div className="flex items-center gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Download ${a.fileName}`}
                          onClick={() => download(a)}
                        >
                          <Download className="h-4 w-4" />
                        </Button>
                        {remove && !readOnly && (
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={`Remove ${a.fileName}`}
                            onClick={() => doRemove.mutate(a.id)}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Attach a file</DialogTitle>
            <DialogDescription>
              The file is scanned before it is stored. A refusal comes back with a reason.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="attachment-file">File</Label>
              <Input
                id="attachment-file"
                type="file"
                ref={fileInputRef}
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="attachment-description">Description</Label>
              <Input
                id="attachment-description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Optional"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => doUpload.mutate()} disabled={!file || doUpload.isPending}>
              {doUpload.isPending ? 'Uploading…' : 'Upload'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
