'use client';

import { useEffect, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Camera, Loader2, User } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/hooks/use-toast';
import { hrDocumentService } from '@/services/hr/hr-document.service';

/**
 * Loads a gated image (one that needs the bearer token, so an `<img src>` cannot fetch it) into an
 * object URL, and revokes it when the endpoint changes or the component unmounts.
 *
 * ⚠ `version` exists so a caller can force a refetch after an upload to the SAME endpoint — the
 * URL does not change when the photo does.
 */
export function useGatedImage(endpoint: string | null, version = 0) {
  const [url, setUrl] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let active = true;
    let objectUrl: string | null = null;
    setUrl(null);
    setFailed(false);
    if (!endpoint) return;

    hrDocumentService
      .fetchObjectUrl(endpoint)
      .then((u) => {
        if (!active) { URL.revokeObjectURL(u); return; }
        objectUrl = u;
        setUrl(u);
      })
      .catch(() => { if (active) setFailed(true); });

    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
    // version is a deliberate dependency: same endpoint, new bytes.
  }, [endpoint, version]);

  return { url, failed };
}

/**
 * A round photograph fetched through the gate, with an initials fallback. Used wherever a face
 * belongs beside a name — the profile header, the guarantor row, the dependant row.
 */
export function GatedPhoto({
  endpoint,
  enabled,
  version = 0,
  alt,
  className = 'h-10 w-10',
}: {
  endpoint: string;
  enabled: boolean;
  version?: number;
  alt: string;
  className?: string;
}) {
  const { url } = useGatedImage(enabled ? endpoint : null, version);
  return (
    <div className={`${className} flex shrink-0 items-center justify-center overflow-hidden rounded-full bg-muted`}>
      {url ? (
        // eslint-disable-next-line @next/next/no-img-element -- object URL, not a remote image
        <img src={url} alt={alt} className="h-full w-full object-cover" />
      ) : (
        <User className="h-1/2 w-1/2 text-muted-foreground" aria-label={alt} />
      )}
    </div>
  );
}

/**
 * The body of the photo dialog, exported on its own so a larger dialog (the guarantor's files)
 * can embed it beside other things without nesting dialogs.
 */
export function PhotoPanel({
  endpoint,
  hasPhoto,
  upload,
  onUploaded,
  subjectLabel,
  canWrite = true,
}: {
  /** The gated GET that answers the current photograph inline. */
  endpoint: string;
  /** Whether the record says a photograph is on file — spares a 404 round-trip when it is not. */
  hasPhoto: boolean;
  upload: (file: File) => Promise<unknown>;
  onUploaded?: () => void;
  subjectLabel: string;
  canWrite?: boolean;
}) {
  const { toast } = useToast();
  const [file, setFile] = useState<File | null>(null);
  const [version, setVersion] = useState(0);
  // The record's flag says whether to try; after a successful upload we know there is one even
  // before the parent refetches its list.
  const [present, setPresent] = useState(hasPhoto);
  useEffect(() => setPresent(hasPhoto), [hasPhoto]);

  const { url, failed } = useGatedImage(present ? endpoint : null, version);

  const save = useMutation({
    mutationFn: () => {
      if (!file) throw new Error('Choose an image first.');
      return upload(file);
    },
    onSuccess: () => {
      toast({ title: 'Photograph saved' });
      setFile(null);
      setPresent(true);
      setVersion((v) => v + 1);
      onUploaded?.();
    },
    onError: (e: unknown) => toast({
      variant: 'destructive',
      title: 'Not saved',
      description: e instanceof Error ? e.message : 'The upload was refused.',
    }),
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-4">
        <div className="flex h-28 w-28 shrink-0 items-center justify-center overflow-hidden rounded-lg border bg-muted">
          {url ? (
            // eslint-disable-next-line @next/next/no-img-element -- object URL, not a remote image
            <img src={url} alt={subjectLabel} className="h-full w-full object-cover" />
          ) : (
            <Camera className="h-8 w-8 text-muted-foreground" />
          )}
        </div>
        <div className="text-sm text-muted-foreground">
          {present && !failed
            ? 'A photograph is on file. Choosing another replaces it.'
            : failed
              ? 'The photograph on file could not be loaded.'
              : 'No photograph on file yet.'}
        </div>
      </div>
      {canWrite && (
        <div className="space-y-2">
          <Label>Image</Label>
          <Input
            type="file"
            accept="image/*"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
          <p className="text-xs text-muted-foreground">
            Virus-scanned and registered in the document store; whoever uploads it is recorded.
          </p>
          <Button onClick={() => save.mutate()} disabled={save.isPending || !file} size="sm">
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {present ? 'Replace photograph' : 'Upload photograph'}
          </Button>
        </div>
      )}
    </div>
  );
}

/** View and replace one record's photograph through its gated endpoint. */
export function PhotoDialog({
  open,
  onOpenChange,
  title,
  description,
  ...panel
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
} & Parameters<typeof PhotoPanel>[0]) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>
        <PhotoPanel {...panel} />
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Close</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
