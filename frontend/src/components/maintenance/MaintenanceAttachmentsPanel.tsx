'use client';

import React from 'react';
import { Eye, Download, Trash2, Upload, Paperclip, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/hooks/use-toast';

import {
  maintenanceAttachmentsService,
  MaintenanceAttachmentDto,
  MaintenanceAttachmentEntityType,
} from '@/services/maintenanceAttachmentsService';

type Props = {
  entityType: MaintenanceAttachmentEntityType;
  entityId: string;
  category?: string;
  title?: string;
  description?: string;
  readOnly?: boolean;
};

function isPreviewable(contentType: string): 'image' | 'pdf' | 'html' | 'text' | null {
  if (contentType.startsWith('image/')) return 'image';
  if (contentType === 'application/pdf') return 'pdf';
  if (contentType === 'text/html') return 'html';
  if (contentType.startsWith('text/')) return 'text';
  return null;
}

function safeFileName(name: string) {
  return (name || 'file').replace(/[\\/:*?"<>|]+/g, '-');
}

export default function MaintenanceAttachmentsPanel({ entityType, entityId, category, title, description, readOnly }: Props) {
  const { toast } = useToast();
  const fileInputRef = React.useRef<HTMLInputElement>(null);

  const [loading, setLoading] = React.useState(false);
  const [uploading, setUploading] = React.useState(false);
  const [attachments, setAttachments] = React.useState<MaintenanceAttachmentDto[]>([]);

  const [previewOpen, setPreviewOpen] = React.useState(false);
  const [previewTitle, setPreviewTitle] = React.useState<string>('');
  const [previewKind, setPreviewKind] = React.useState<'image' | 'pdf' | 'html' | 'text' | null>(null);
  const [previewUrl, setPreviewUrl] = React.useState<string>('');

  const [deleteOpen, setDeleteOpen] = React.useState(false);
  const [deleting, setDeleting] = React.useState<MaintenanceAttachmentDto | null>(null);

  const load = React.useCallback(async () => {
    if (!entityId) return;
    setLoading(true);
    try {
      const list = await maintenanceAttachmentsService.list(entityType, entityId, { category });
      setAttachments(list || []);
    } catch (e: any) {
      toast({ title: 'Failed to load attachments', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [entityId, entityType, category, toast]);

  React.useEffect(() => {
    load();
  }, [load]);

  React.useEffect(() => {
    return () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl);
    };
  }, [previewUrl]);

  const handleUploadSelected = async (files: FileList | null) => {
    if (!files || files.length === 0) return;
    if (readOnly) return;

    const list = Array.from(files);
    setUploading(true);
    try {
      await maintenanceAttachmentsService.upload(entityType, entityId, list, { category });
      toast({ title: 'Uploaded' });
      await load();
    } catch (e: any) {
      toast({ title: 'Upload failed', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  const handleDownload = async (a: MaintenanceAttachmentDto) => {
    try {
      const blob = await maintenanceAttachmentsService.downloadBlob(a.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = safeFileName(a.fileName);
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (e: any) {
      toast({ title: 'Download failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const handleView = async (a: MaintenanceAttachmentDto) => {
    try {
      const kind = isPreviewable(a.contentType);
      if (!kind) {
        await handleDownload(a);
        return;
      }

      const blob = await maintenanceAttachmentsService.downloadBlob(a.id);
      const url = URL.createObjectURL(blob);
      if (previewUrl) URL.revokeObjectURL(previewUrl);

      setPreviewTitle(a.fileName);
      setPreviewKind(kind);
      setPreviewUrl(url);
      setPreviewOpen(true);
    } catch (e: any) {
      toast({ title: 'View failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const startDelete = (a: MaintenanceAttachmentDto) => {
    if (readOnly) return;
    setDeleting(a);
    setDeleteOpen(true);
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    try {
      await maintenanceAttachmentsService.remove(deleting.id);
      toast({ title: 'Deleted' });
      await load();
    } catch (e: any) {
      toast({ title: 'Delete failed', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setDeleteOpen(false);
      setDeleting(null);
    }
  };

  return (
    <div className="space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div className="space-y-1">
          <div className="flex items-center gap-2">
            <Paperclip className="h-4 w-4 text-muted-foreground" />
            <div className="font-medium">{title || 'Attachments'}</div>
            <Badge variant="outline">{attachments.length}</Badge>
          </div>
          {description ? <div className="text-sm text-muted-foreground">{description}</div> : null}
        </div>

        {!readOnly ? (
          <div className="flex items-center gap-2">
            <Label className="sr-only" htmlFor="attachments-upload">
              Upload files
            </Label>
            <Input
              id="attachments-upload"
              ref={fileInputRef}
              type="file"
              multiple
              disabled={uploading}
              onChange={(e) => handleUploadSelected(e.target.files)}
              className="w-[260px]"
            />
            <Button type="button" variant="outline" disabled={uploading} onClick={() => fileInputRef.current?.click()}>
              {uploading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Upload className="mr-2 h-4 w-4" />}
              Upload
            </Button>
          </div>
        ) : null}
      </div>

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/40">
              <TableHead>File</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Size</TableHead>
              <TableHead>Uploaded</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                  Loading...
                </TableCell>
              </TableRow>
            ) : attachments.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                  No attachments
                </TableCell>
              </TableRow>
            ) : (
              attachments.map((a, idx) => (
                <TableRow key={a.id} className={idx % 2 === 1 ? 'bg-muted/10 hover:bg-muted/30' : 'hover:bg-muted/30'}>
                  <TableCell className="max-w-[420px] truncate font-medium" title={a.fileName}>
                    {a.fileName}
                  </TableCell>
                  <TableCell>
                    <Badge variant="secondary">{a.attachmentType}</Badge>
                  </TableCell>
                  <TableCell>{a.fileSizeFormatted || `${a.fileSizeBytes} B`}</TableCell>
                  <TableCell className="text-muted-foreground">{new Date(a.uploadedDate).toLocaleString()}</TableCell>
                  <TableCell className="text-right">
                    <Button variant="ghost" size="sm" onClick={() => handleView(a)}>
                      <Eye className="mr-2 h-4 w-4" />
                      View
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => handleDownload(a)}>
                      <Download className="mr-2 h-4 w-4" />
                      Download
                    </Button>
                    {!readOnly ? (
                      <Button variant="ghost" size="sm" onClick={() => startDelete(a)} className="text-destructive hover:text-destructive">
                        <Trash2 className="mr-2 h-4 w-4" />
                        Delete
                      </Button>
                    ) : null}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      <Dialog
        open={previewOpen}
        onOpenChange={(o) => {
          setPreviewOpen(o);
          if (!o) {
            setPreviewKind(null);
            setPreviewTitle('');
            if (previewUrl) URL.revokeObjectURL(previewUrl);
            setPreviewUrl('');
          }
        }}
      >
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle className="truncate">{previewTitle || 'Preview'}</DialogTitle>
            <DialogDescription>Preview uses a secure download via the API.</DialogDescription>
          </DialogHeader>

          <div className="max-h-[70vh] overflow-auto rounded-md border bg-muted/10 p-3">
            {previewKind === 'image' ? (
               
              <img src={previewUrl} alt={previewTitle} className="mx-auto max-h-[66vh] w-auto rounded" />
            ) : previewKind === 'pdf' || previewKind === 'html' ? (
              <iframe src={previewUrl} className="h-[66vh] w-full rounded bg-white" title={previewTitle} />
            ) : previewKind === 'text' ? (
              <iframe src={previewUrl} className="h-[66vh] w-full rounded bg-white" title={previewTitle} />
            ) : (
              <div className="text-sm text-muted-foreground">Preview not available for this file type.</div>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setPreviewOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={(o) => {
          setDeleteOpen(o);
          if (!o) setDeleting(null);
        }}
        title="Delete attachment?"
        description={deleting ? deleting.fileName : undefined}
        confirmText="Delete"
        variant="destructive"
        onConfirm={confirmDelete}
      />
    </div>
  );
}

