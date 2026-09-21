'use client';

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Download, FileText, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/hooks/use-toast';
import { hrDocumentService } from '@/services/hr/hr-document.service';

const fmtSize = (b?: number | null) =>
  b == null ? '' : b < 1024 ? `${b} B` : b < 1048576 ? `${(b / 1024).toFixed(1)} KB` : `${(b / 1048576).toFixed(1)} MB`;

/**
 * One file on one row — a referee's reference letter, an oath, a signed form. Shows what is on
 * file with a download, and lets the caller replace it through the gate.
 *
 * ⚠ Never an `<a href>` for the download: the file sits outside the web root and the request
 * needs the bearer token. Same helper every other gated HR download uses.
 */
export function AttachFileDialog({
  open,
  onOpenChange,
  title,
  description,
  currentFileName,
  currentFileSize,
  downloadUrl,
  accept,
  upload,
  onUploaded,
  canWrite = true,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  /** What is on file now, if anything. */
  currentFileName?: string | null;
  currentFileSize?: number | null;
  /** The gated GET that streams the current file. */
  downloadUrl?: string;
  accept?: string;
  upload: (file: File) => Promise<unknown>;
  onUploaded?: () => void;
  canWrite?: boolean;
}) {
  const { toast } = useToast();
  const [file, setFile] = useState<File | null>(null);

  const save = useMutation({
    mutationFn: () => {
      if (!file) throw new Error('Choose a file first.');
      return upload(file);
    },
    onSuccess: () => {
      toast({ title: 'File attached' });
      setFile(null);
      onUploaded?.();
      onOpenChange(false);
    },
    onError: (e: unknown) => toast({
      variant: 'destructive',
      title: 'Not attached',
      description: e instanceof Error ? e.message : 'The upload was refused.',
    }),
  });

  const download = async () => {
    if (!downloadUrl) return;
    try {
      await hrDocumentService.download(downloadUrl, currentFileName ?? 'file');
    } catch {
      toast({ variant: 'destructive', title: 'Could not download that file' });
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => { onOpenChange(o); if (!o) setFile(null); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>
        <div className="space-y-4">
          {currentFileName ? (
            <div className="flex items-center justify-between rounded-md border p-3 text-sm">
              <div className="flex min-w-0 items-center gap-2">
                <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />
                <span className="truncate">{currentFileName}</span>
                {currentFileSize != null && (
                  <span className="shrink-0 text-xs text-muted-foreground">{fmtSize(currentFileSize)}</span>
                )}
              </div>
              {downloadUrl && (
                <Button variant="ghost" size="icon" aria-label="Download" onClick={download}>
                  <Download className="h-4 w-4" />
                </Button>
              )}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">Nothing on file yet.</p>
          )}
          {canWrite && (
            <div className="space-y-2">
              <Label>{currentFileName ? 'Replace with' : 'File'}</Label>
              <Input type="file" accept={accept} onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
              <p className="text-xs text-muted-foreground">
                Virus-scanned and registered in the document store; whoever attaches it is recorded.
              </p>
            </div>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Close</Button>
          {canWrite && (
            <Button onClick={() => save.mutate()} disabled={save.isPending || !file}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Attach
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
