'use client';

import { useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Loader2, Paperclip, Trash2, Upload } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/hooks/use-toast';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { formatDateTime } from '@/lib/hr/attendance-format';
import { unionService } from '@/services/hr/union.service';
import {
  UNION_DOCUMENT_KINDS,
  type CollectiveBargainingAgreement,
  type UnionDocument,
  type UnionDocumentKind,
} from '@/types/hr/union';

interface UnionDocumentsPanelProps {
  unionId: string;
  unionName: string;
  /** The union's agreements — the picker source for "which agreement is this the signed copy of". */
  agreements: CollectiveBargainingAgreement[];
  canWrite: boolean;
  /** Document DELETE is HR admin only; the HR actor is refused. */
  canDelete: boolean;
  /** Preselects the agreement (and forces the kind) when opened from an agreement row. */
  onChanged?: () => Promise<unknown> | void;
}

const NONE = '__none__';

/**
 * A union's files (round 3, lane U; register row U-2): the signed collective agreement, the
 * constitution, correspondence, a membership list.
 *
 * The shape is `AttachmentsPanel`'s — same gate, same gated download, same "no href to a stored
 * path" rule — but a union document carries two things a plain attachment does not: a KIND and,
 * for the signed copy, the AGREEMENT it belongs to. `AttachmentsPanel`'s dialog has no room for
 * either, so this panel owns its dialog and keeps the table identical.
 *
 * ⚠ Server rules the dialog mirrors rather than fights: naming an agreement makes the file that
 * agreement's copy (kind forced to CollectiveAgreement); kind CollectiveAgreement WITHOUT an
 * agreement is refused; the agreement must be this union's. The category is scan-mandatory — with
 * no scanner the gate answers 422 with a reason and stores nothing.
 */
export function UnionDocumentsPanel({
  unionId,
  unionName,
  agreements,
  canWrite,
  canDelete,
  onChanged,
}: UnionDocumentsPanelProps) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const queryKey = ['hr', 'unions', unionId, 'documents'] as const;

  const { data, isLoading } = useQuery({ queryKey, queryFn: () => unionService.getDocuments(unionId) });
  const documents = data ?? [];

  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [kind, setKind] = useState<UnionDocumentKind>('Other');
  const [agreementId, setAgreementId] = useState<string>(NONE);
  const [description, setDescription] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey });
    await onChanged?.();
  };

  const reset = () => {
    setFile(null);
    setKind('Other');
    setAgreementId(NONE);
    setDescription('');
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const agreementRequired = kind === 'CollectiveAgreement';
  const canUpload = !!file && (!agreementRequired || agreementId !== NONE);

  const upload = useMutation({
    mutationFn: () => {
      if (!file) throw new Error('Choose a file first.');
      return unionService.uploadDocument(unionId, file, {
        kind,
        agreementId: agreementId === NONE ? null : agreementId,
        description: description.trim() || null,
      });
    },
    onSuccess: async () => {
      await refresh();
      setOpen(false);
      reset();
      toast({ title: 'File attached' });
    },
    // The gate answers { code, message } — the message is the actionable part.
    onError: (e: unknown) =>
      toast({
        variant: 'destructive',
        title: 'Upload refused',
        description: (e as Error)?.message ?? 'Please try again.',
      }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => unionService.removeDocument(id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Document removed' });
    },
    onError: (e: unknown) =>
      toast({
        variant: 'destructive',
        title: 'Could not remove the document',
        description: (e as Error)?.message ?? 'Please try again.',
      }),
  });

  const kindLabel = useMemo(
    () => Object.fromEntries(UNION_DOCUMENT_KINDS.map((k) => [k.value, k.label.replace(' (signed copy)', '')])),
    [],
  );

  const download = (d: UnionDocument) =>
    unionService.downloadDocument(unionId, d).catch((e: unknown) =>
      toast({
        variant: 'destructive',
        title: 'Could not download the file',
        description: e instanceof Error ? e.message : 'Please try again.',
      }),
    );

  return (
    <>
      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-4">
          <div className="space-y-1.5">
            <CardTitle>Documents</CardTitle>
            <CardDescription>
              The signed collective agreement, the constitution, correspondence with {unionName}.
              Files are scanned and stored in the document repository; they are never public links.
            </CardDescription>
          </div>
          {canWrite && (
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
          ) : documents.length === 0 ? (
            <EmptyState
              icon={Paperclip}
              title="No files"
              description="Upload the signed collective agreement against its agreement, and anything else worth keeping on the union's record."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>File</TableHead>
                  <TableHead className="w-44">Kind</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead className="w-40">Uploaded by</TableHead>
                  <TableHead className="w-40">When</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {documents.map((d) => (
                  <TableRow key={d.id} data-testid="union-document-row">
                    <TableCell className="font-medium">{d.fileName}</TableCell>
                    <TableCell>
                      <div className="flex flex-col gap-1">
                        <Badge variant="outline" className="w-fit">
                          {kindLabel[d.kind] ?? d.kindName}
                        </Badge>
                        {d.agreementTitle && (
                          <span className="text-xs text-muted-foreground">{d.agreementTitle}</span>
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="text-muted-foreground">{d.description ?? '—'}</TableCell>
                    <TableCell>{d.uploadedByName || '—'}</TableCell>
                    <TableCell>{formatDateTime(d.uploadDate)}</TableCell>
                    <TableCell>
                      <div className="flex items-center gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Download ${d.fileName}`}
                          onClick={() => download(d)}
                        >
                          <Download className="h-4 w-4" />
                        </Button>
                        {canDelete && (
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={`Remove ${d.fileName}`}
                            onClick={() => remove.mutate(d.id)}
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

      <Dialog
        open={open}
        onOpenChange={(o) => {
          setOpen(o);
          if (!o) reset();
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Attach a file</DialogTitle>
            <DialogDescription>
              The file is scanned before it is stored. A refusal comes back with a reason.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="union-document-file">File</Label>
              <Input
                id="union-document-file"
                type="file"
                ref={fileInputRef}
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="union-document-kind">Kind</Label>
              <Select
                value={kind}
                onValueChange={(v) => {
                  setKind(v as UnionDocumentKind);
                  // A kind other than the signed copy does not belong to an agreement.
                  if (v !== 'CollectiveAgreement') setAgreementId(NONE);
                }}
              >
                <SelectTrigger id="union-document-kind">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {UNION_DOCUMENT_KINDS.map((k) => (
                    <SelectItem key={k.value} value={k.value}>
                      {k.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {agreementRequired && (
              <div className="space-y-2">
                <Label htmlFor="union-document-agreement">Agreement</Label>
                <Select value={agreementId} onValueChange={setAgreementId}>
                  <SelectTrigger id="union-document-agreement">
                    <SelectValue placeholder="Which agreement is this the signed copy of?" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE} disabled>
                      Choose an agreement…
                    </SelectItem>
                    {agreements.map((a) => (
                      <SelectItem key={a.id} value={a.id}>
                        {a.title}
                        {a.referenceNumber ? ` · ${a.referenceNumber}` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {agreements.length === 0 && (
                  <p className="text-xs text-destructive">
                    Record the agreement on the Agreements tab first; the signed copy is filed
                    against it.
                  </p>
                )}
              </div>
            )}
            <div className="space-y-2">
              <Label htmlFor="union-document-description">Description</Label>
              <Input
                id="union-document-description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Optional"
                maxLength={500}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={upload.isPending}>
              Cancel
            </Button>
            <Button onClick={() => upload.mutate()} disabled={!canUpload || upload.isPending}>
              {upload.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {upload.isPending ? 'Uploading…' : 'Upload'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
