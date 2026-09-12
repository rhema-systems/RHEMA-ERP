'use client';

import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, FileWarning, Loader2, Paperclip, ShieldAlert, Trash2, Upload } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import {
  successionCandidateService,
  successionDocumentService,
  successionService,
  talentPoolService,
} from '@/services/hr/succession.service';
import type { SuccessionDocument, SuccessionDocumentOwner } from '@/types/hr/succession';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const MAX_BYTES = 10 * 1024 * 1024;
const fmtSize = (b: number) =>
  b >= 1_048_576 ? `${(b / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(b / 1024))} KB`;

/**
 * Free text server-side (`documentType` is a 100-character string, not an enum), so these are
 * suggestions rather than a closed set — but a picker keeps one plan's files sortable.
 */
const DOCUMENT_TYPES = [
  'AssessmentReport',
  'DevelopmentPlan',
  'InterviewNotes',
  'BoardPaper',
  'Correspondence',
  'Other',
];

type Owner =
  | { kind: 'plan'; id: string }
  | { kind: 'candidate'; id: string }
  | { kind: 'member'; id: string };

const ownerField = (owner: Owner): SuccessionDocumentOwner =>
  owner.kind === 'plan'
    ? { successionPlanId: owner.id }
    : owner.kind === 'candidate'
      ? { candidateId: owner.id }
      : { talentPoolMemberId: owner.id };

/**
 * Succession documents — one panel for all three owners.
 *
 * ⚠ **This collection could not be built at all until the upload endpoint existed.** The only
 * create route took a `[Required]` caller-supplied `DocumentUrl` and stored it verbatim, so any
 * HR user could point a document row at arbitrary bytes on disk — the fourth instance of that
 * sink — and there was no download route either, so the row was unreadable even when the path
 * was honest. Files now go through the scanning + DMS gate (D-14) and the uploader is stamped
 * from the token rather than the request body (D-15).
 *
 * ⚠ **The metadata-only POST on each parent stays unwired on purpose.** It refuses `documentUrl`
 * and the three DMS ids and says so in its 400, so through the API it can only mint a row naming
 * a file the server never received. It survives for the legacy migration utility.
 *
 * ⚠ **A confidential document is a separate list, not a filter.** `documents/confidential` is its
 * own Admin-only read, so a plan's ordinary list does not contain them — which is why this panel
 * shows the two lists together only when the caller holds Admin, and why the download route
 * re-checks the flag rather than trusting whichever list the row came from.
 */
export function SuccessionDocumentsPanel({
  owner,
  canWrite,
  canDelete,
  title = 'Documents',
  /** Only a succession plan has a confidential list; candidates and pool members do not. */
  showConfidential = false,
}: {
  owner: Owner;
  canWrite: boolean;
  canDelete: boolean;
  title?: string;
  showConfidential?: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const fileInput = useRef<HTMLInputElement>(null);

  const queryKey = ['succession-documents', owner.kind, owner.id];
  const { data: documents, isLoading } = useQuery({
    queryKey,
    queryFn: () =>
      owner.kind === 'plan'
        ? successionService.getDocuments(owner.id)
        : owner.kind === 'candidate'
          ? successionCandidateService.getDocuments(owner.id)
          : talentPoolService.getMemberDocuments(owner.id),
    enabled: !!owner.id,
  });

  const confidentialKey = ['succession-documents', owner.kind, owner.id, 'confidential'];
  const { data: confidential } = useQuery({
    queryKey: confidentialKey,
    queryFn: () => successionService.getConfidentialDocuments(owner.id),
    // Admin-only; a caller without it gets a 403 rather than an empty list, so do not ask.
    enabled: showConfidential && owner.kind === 'plan' && canDelete,
  });

  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [documentName, setDocumentName] = useState('');
  const [documentType, setDocumentType] = useState(DOCUMENT_TYPES[0]);
  const [description, setDescription] = useState('');
  const [isConfidential, setIsConfidential] = useState(false);
  const [retentionDate, setRetentionDate] = useState('');
  const [pendingDelete, setPendingDelete] = useState<SuccessionDocument | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);

  const reset = () => {
    setFile(null);
    setDocumentName('');
    setDocumentType(DOCUMENT_TYPES[0]);
    setDescription('');
    setIsConfidential(false);
    setRetentionDate('');
    if (fileInput.current) fileInput.current.value = '';
  };

  const fail = (title: string) => (e: any) =>
    toast({
      title,
      // The gate answers its own {code, message} contract; hrDocumentService normalizes it.
      description: e?.message ?? e?.response?.data?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey });
    await queryClient.invalidateQueries({ queryKey: confidentialKey });
    if (owner.kind === 'plan') {
      await queryClient.invalidateQueries({ queryKey: ['succession-plans', owner.id] });
    }
  };

  const upload = useMutation({
    mutationFn: () =>
      successionDocumentService.upload(file as File, ownerField(owner), {
        documentName: documentName.trim() || (file as File).name,
        documentType,
        description: description.trim() || null,
        isConfidential,
        retentionDate: retentionDate ? new Date(`${retentionDate}T00:00:00`).toISOString() : null,
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Document attached' });
      setOpen(false);
      reset();
    },
    onError: fail('Could not attach the document'),
  });

  const remove = useMutation({
    mutationFn: (doc: SuccessionDocument) =>
      owner.kind === 'plan'
        ? successionService.removeDocument(doc.id)
        : owner.kind === 'candidate'
          ? successionCandidateService.removeDocument(doc.id)
          : talentPoolService.removeMemberDocument(doc.id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Document removed' });
      setPendingDelete(null);
    },
    onError: fail('Could not remove the document'),
  });

  const download = async (doc: SuccessionDocument) => {
    setDownloading(doc.id);
    try {
      await successionDocumentService.download(doc.id, doc.documentName);
    } catch (e) {
      fail('Could not download the document')(e);
    } finally {
      setDownloading(null);
    }
  };

  const tooBig = !!file && file.size > MAX_BYTES;
  // The confidential list is a separate read, so the two are concatenated rather than filtered.
  const rows = [...(documents ?? []), ...(confidential ?? [])];

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base">{title}</CardTitle>
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
              description={
                showConfidential && !canDelete
                  ? 'Nothing here. Confidential documents are a separate, administrator-only list and do not appear on this one.'
                  : 'Nothing is held here — no assessment report, board paper or development plan. Files are scanned and stored privately.'
              }
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Document</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Uploaded by</TableHead>
                <TableHead>Uploaded</TableHead>
                <TableHead>Retain until</TableHead>
                <TableHead className="w-24 text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((d) => {
                // No scanned file behind it — a row that predates the upload gate. Its
                // documentUrl names a file the server never received, so there is nothing to
                // stream and the download would fail.
                const legacy = !d.fileUploadRecordId && !d.documentRecordId;
                return (
                  <TableRow key={d.id}>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <span className="font-medium">{d.documentName}</span>
                        {d.isConfidential && (
                          <Badge variant="outline" className="gap-1">
                            <ShieldAlert className="h-3 w-3" />
                            Confidential
                          </Badge>
                        )}
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
                      {!legacy && d.fileSizeBytes > 0 && (
                        <div className="text-xs text-muted-foreground">
                          {fmtSize(d.fileSizeBytes)}
                        </div>
                      )}
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline">{d.documentType}</Badge>
                    </TableCell>
                    <TableCell>{d.uploadedByName || '—'}</TableCell>
                    <TableCell>{fmtDate(d.uploadDate)}</TableCell>
                    <TableCell>{fmtDate(d.retentionDate)}</TableCell>
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
      </CardContent>

      <Dialog open={open} onOpenChange={(o) => { setOpen(o); if (!o) reset(); }}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>Attach a succession document</DialogTitle>
            <DialogDescription>
              The file is scanned and stored privately. It can only be read back through this
              screen, and you are recorded as its uploader.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="sd-file">
                File<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Input
                id="sd-file"
                type="file"
                ref={fileInput}
                onChange={(e) => {
                  const picked = e.target.files?.[0] ?? null;
                  setFile(picked);
                  if (picked && !documentName.trim()) setDocumentName(picked.name);
                }}
              />
              {file && (
                <p className={`text-xs ${tooBig ? 'text-red-500' : 'text-muted-foreground'}`}>
                  {fmtSize(file.size)}
                  {tooBig && ' — over the 10 MB limit'}
                </p>
              )}
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="sd-name">
                  Name<span className="ml-0.5 text-red-500">*</span>
                </Label>
                <Input
                  id="sd-name"
                  maxLength={200}
                  value={documentName}
                  onChange={(e) => setDocumentName(e.target.value)}
                  placeholder="What this document is called"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="sd-type">Type</Label>
                <Select value={documentType} onValueChange={setDocumentType}>
                  <SelectTrigger id="sd-type"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {DOCUMENT_TYPES.map((t) => (
                      <SelectItem key={t} value={t}>
                        {t.replace(/([a-z])([A-Z])/g, '$1 $2')}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="sd-description">Description</Label>
              <Input
                id="sd-description"
                maxLength={1000}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Optional — what this document is for"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="sd-retention">Retain until</Label>
              <Input
                id="sd-retention"
                type="date"
                value={retentionDate}
                onChange={(e) => setRetentionDate(e.target.value)}
              />
            </div>

            <div className="flex items-start gap-2 rounded-md border p-3">
              <Checkbox
                id="sd-confidential"
                checked={isConfidential}
                onCheckedChange={(v) => setIsConfidential(v === true)}
              />
              <div className="space-y-1">
                <Label htmlFor="sd-confidential" className="cursor-pointer">
                  Confidential
                </Label>
                <p className="text-xs text-muted-foreground">
                  Moves it off the ordinary list onto the administrator-only one, and restricts the
                  download to administrators. Board papers and external assessments usually belong
                  here.
                </p>
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={upload.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() => upload.mutate()}
              disabled={!file || tooBig || !documentName.trim() || upload.isPending}
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
        description={`"${pendingDelete?.documentName ?? ''}" will be removed. The stored file stays in the document repository.`}
        confirmText="Remove"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={async () => {
          if (pendingDelete) await remove.mutateAsync(pendingDelete);
        }}
      />
    </Card>
  );
}
