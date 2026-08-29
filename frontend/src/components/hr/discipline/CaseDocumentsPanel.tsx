'use client';

import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Loader2, Paperclip, Trash2, Upload } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { disciplineDocumentService, disciplineActionStepService } from '@/services/hr/discipline.service';
import {
  DOCUMENT_CATEGORY_OPTIONS,
  DOCUMENT_SCOPE_OPTIONS,
  type DisciplinaryDocumentCategory,
  type DisciplinaryDocumentScope,
  type DisciplineDocument,
} from '@/types/hr/discipline';

const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

const labelFor = <T extends string>(options: { value: T; label: string }[], v?: T | null) =>
  options.find((o) => o.value === v)?.label ?? (v ?? '—');

/** The server's own cap. Checked here so a 10 MB+ file fails before it is uploaded, not after. */
const MAX_BYTES = 10 * 1024 * 1024;

const fmtSize = (bytes: number) =>
  bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;

/**
 * Documents on the case.
 *
 * Purpose-built rather than using `ResourceCollectionTab` for two reasons the shared panel cannot
 * express: the create is a **multipart upload**, not a JSON body, and there is no update at all —
 * a document is replaced by uploading another, never edited.
 *
 * ⚠ **Everything goes through the controlled gate.** The file is scanned, registered in the DMS and
 * stored outside the web root. The second route, `POST cases/{id}/documents`, is deliberately not
 * offered: it rejects every file-location field, so through the API it can only mint a row naming a
 * file that does not exist.
 *
 * ⚠ **`filePath` is a server-side location, not a URL.** Download is a token-bearing fetch that
 * returns a blob; rendering `filePath` as an `href` would produce a dead link that also leaks the
 * storage layout. `fileName` is what the user sees and what the saved file is called.
 *
 * ⚠ **Scope is validated before any bytes are stored.** `ActionStep` demands a step id and `Appeal`
 * an appeal id, so the scope select drives a second required field rather than standing alone.
 * The appeal option only appears when the case actually has an appeal.
 */
export function CaseDocumentsPanel({
  caseId,
  appealId,
  canWrite,
  canDelete,
  onChanged,
}: {
  caseId: string;
  appealId?: string | null;
  canWrite: boolean;
  canDelete: boolean;
  onChanged?: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const fileInput = useRef<HTMLInputElement>(null);

  const queryKey = ['hr', 'discipline', caseId, 'documents'];
  const { data: documents, isLoading } = useQuery({
    queryKey,
    queryFn: () => disciplineDocumentService.getForCase(caseId),
    enabled: !!caseId,
  });

  // Only needed to fill the step picker when the scope is ActionStep.
  const { data: steps } = useQuery({
    queryKey: ['hr', 'discipline', caseId, 'action-steps'],
    queryFn: () => disciplineActionStepService.getForCase(caseId),
    enabled: !!caseId,
  });

  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [scope, setScope] = useState<DisciplinaryDocumentScope>('Case');
  const [category, setCategory] = useState<DisciplinaryDocumentCategory>('Evidence');
  const [actionStepId, setActionStepId] = useState('');
  const [description, setDescription] = useState('');
  const [pendingDelete, setPendingDelete] = useState<DisciplineDocument | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);

  const scopeOptions = DOCUMENT_SCOPE_OPTIONS.filter(
    (o) => o.value !== 'Appeal' || !!appealId,
  );

  const reset = () => {
    setFile(null);
    setScope('Case');
    setCategory('Evidence');
    setActionStepId('');
    setDescription('');
    if (fileInput.current) fileInput.current.value = '';
  };

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey });
    onChanged?.();
  };

  const upload = useMutation({
    mutationFn: () =>
      disciplineDocumentService.upload(caseId, file as File, {
        scope,
        category,
        actionStepId: scope === 'ActionStep' ? actionStepId : null,
        appealId: scope === 'Appeal' ? (appealId ?? null) : null,
        description: description.trim() || null,
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Document attached' });
      setOpen(false);
      reset();
    },
    onError: (e: any) =>
      toast({
        title: 'Could not attach the document',
        // The gate's refusals are the useful ones — an infected or oversized file says so.
        description: e?.response?.data?.message ?? e?.message ?? 'Please try again.',
        variant: 'destructive',
      }),
  });

  const remove = useMutation({
    mutationFn: (doc: DisciplineDocument) => disciplineDocumentService.remove(doc.id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Document removed' });
      setPendingDelete(null);
    },
    onError: (e: any) =>
      toast({
        title: 'Could not remove the document',
        description: e?.response?.data?.message ?? e?.message ?? 'Please try again.',
        variant: 'destructive',
      }),
  });

  const download = async (doc: DisciplineDocument) => {
    setDownloading(doc.id);
    try {
      await disciplineDocumentService.download(doc.id, doc.fileName);
    } catch (e: any) {
      toast({
        title: 'Could not download the document',
        description: e?.response?.data?.message ?? e?.message ?? 'Please try again.',
        variant: 'destructive',
      });
    } finally {
      setDownloading(null);
    }
  };

  const tooBig = !!file && file.size > MAX_BYTES;
  const needsStep = scope === 'ActionStep' && !actionStepId;
  const rows = documents ?? [];

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle>Documents</CardTitle>
        {canWrite && (
          <Button size="sm" onClick={() => { reset(); setOpen(true); }}>
            <Upload className="mr-2 h-4 w-4" />
            Attach a document
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {isLoading ? (
          <div className="flex justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <div className="px-6 pb-6">
            <EmptyState
              icon={Paperclip}
              title="No documents"
              description="Nothing has been attached to this case. Files are scanned and stored privately; they are never served from a public path."
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>File</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Attached to</TableHead>
                <TableHead>Uploaded</TableHead>
                <TableHead>By</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((d) => (
                <TableRow key={d.id}>
                  <TableCell>
                    <div>{d.fileName}</div>
                    {d.description && (
                      <div className="text-xs text-muted-foreground">{d.description}</div>
                    )}
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline">
                      {d.categoryName ?? labelFor(DOCUMENT_CATEGORY_OPTIONS, d.category)}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {d.scope === 'ActionStep'
                      ? (d.actionStepName ?? 'A procedure step')
                      : d.scope === 'Appeal'
                        ? 'The appeal'
                        : 'The case'}
                  </TableCell>
                  <TableCell>{fmtDateTime(d.uploadDate)}</TableCell>
                  <TableCell>{d.uploadedByName || '—'}</TableCell>
                  <TableCell className="text-right">
                    <div className="flex justify-end gap-1">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => download(d)}
                        disabled={downloading === d.id}
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
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={open} onOpenChange={(o) => { setOpen(o); if (!o) reset(); }}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>Attach a document</DialogTitle>
            <DialogDescription>
              The file is scanned and stored privately. It can only be read back through this
              screen by someone entitled to see the case.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="doc-file">File<span className="ml-0.5 text-red-500">*</span></Label>
              <Input
                id="doc-file"
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

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="doc-category">Category</Label>
                <Select value={category} onValueChange={(v) => setCategory(v as DisciplinaryDocumentCategory)}>
                  <SelectTrigger id="doc-category"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {DOCUMENT_CATEGORY_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="doc-scope">Attach to</Label>
                <Select
                  value={scope}
                  onValueChange={(v) => { setScope(v as DisciplinaryDocumentScope); setActionStepId(''); }}
                >
                  <SelectTrigger id="doc-scope"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {scopeOptions.map((o) => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            {/* Required by the server when the scope is ActionStep — refused before any bytes land. */}
            {scope === 'ActionStep' && (
              <div className="space-y-2">
                <Label htmlFor="doc-step">Which step<span className="ml-0.5 text-red-500">*</span></Label>
                <Select value={actionStepId} onValueChange={setActionStepId}>
                  <SelectTrigger id="doc-step">
                    <SelectValue placeholder={(steps ?? []).length ? 'Choose a step' : 'The procedure has no steps yet'} />
                  </SelectTrigger>
                  <SelectContent>
                    {(steps ?? [])
                      .slice()
                      .sort((a, b) => a.sequence - b.sequence)
                      .map((st) => (
                        <SelectItem key={st.id} value={st.id}>
                          {st.sequence}. {st.stepName}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            <div className="space-y-2">
              <Label htmlFor="doc-description">Description</Label>
              <Input
                id="doc-description"
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
            <Button
              onClick={() => upload.mutate()}
              disabled={!file || tooBig || needsStep || upload.isPending}
            >
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
        description={`"${pendingDelete?.fileName ?? ''}" will be removed from the case.`}
        confirmText="Remove"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={async () => { if (pendingDelete) await remove.mutateAsync(pendingDelete); }}
      />
    </Card>
  );
}
