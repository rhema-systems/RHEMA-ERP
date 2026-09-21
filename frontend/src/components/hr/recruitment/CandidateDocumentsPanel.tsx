'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, FileText, IdCard, Loader2, MailCheck, Trash2, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import { jobCandidateService } from '@/services/hr/recruitment-pipeline.service';
import {
  CANDIDATE_GENERIC_DOCUMENT_TYPES,
  type CandidateDocument,
  type CandidateDocumentType,
} from '@/types/hr/recruitment-pipeline';

/** The description a reference letter carries, so the letter names the referee it vouches for. */
export const referenceLetterDescription = (refereeName: string) => `Reference letter from ${refereeName}`;
/** The description an ID scan carries — the document it is a scan of. */
export const idScanDescription = (identityLabel: string | null | undefined) =>
  identityLabel ? `ID document — ${identityLabel}` : 'ID document';

type UploadKind = 'generic' | 'id' | 'reference';

/**
 * Candidate documents — CVs, transcripts, certificates — plus the two that no longer sit in the
 * generic dropdown (round 3, lane C2; decision D-17): the ID scan, attached from the identity
 * document, and reference letters, each attached from the referee it vouches for.
 *
 * ⚠ There is no column tying a letter to a referee this round (the lane carries no migration).
 * The tie is the row's `description`, written by this panel as "Reference letter from <name>", and
 * the ID scan's as "ID document — <type number>". The careers profile writes the same shapes, so
 * both sides read each other's rows. A referee link column is the follow-on if the demo asks.
 *
 * ⚠ Uploads are **multipart through the controlled-upload gate**: the file is virus-scanned and
 * registered in the central DMS before the row is written. On a machine with no ClamAV the gate
 * refuses with 422 before anything is written, so a local upload failure here is usually an
 * environment problem, not a defect. Downloads are always blob fetches with the bearer token.
 */
export function CandidateDocumentsPanel({
  candidateId,
  canEdit,
  identityLabel,
  referees = [],
}: {
  candidateId: string;
  canEdit: boolean;
  /** "<type> <number>" of the national-ID trio, when recorded — names the ID scan. */
  identityLabel?: string | null;
  /** The candidate's referees, so a reference letter can be attached to one. */
  referees?: { id: string; fullName: string }[];
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [uploading, setUploading] = useState<UploadKind | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [documentType, setDocumentType] = useState<CandidateDocumentType>('Resume');
  const [description, setDescription] = useState('');
  const [refereeId, setRefereeId] = useState('');
  const [deleting, setDeleting] = useState<CandidateDocument | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'candidate-documents', candidateId],
    queryFn: () => jobCandidateService.getDocuments(candidateId),
    enabled: !!candidateId,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'candidate-documents', candidateId] });

  const open = (kind: UploadKind) => {
    setFile(null);
    setDescription('');
    setRefereeId(referees[0]?.id ?? '');
    setDocumentType('Resume');
    setUploading(kind);
  };

  const resolved = (): { type: CandidateDocumentType; description: string | null } => {
    if (uploading === 'id') return { type: 'IdDocument', description: idScanDescription(identityLabel) };
    if (uploading === 'reference') {
      const referee = referees.find((r) => r.id === refereeId);
      return {
        type: 'ReferenceLetter',
        description: referee ? referenceLetterDescription(referee.fullName) : 'Reference letter',
      };
    }
    return { type: documentType, description: description.trim() || null };
  };

  const upload = useMutation({
    mutationFn: () => {
      if (!file) return Promise.reject(new Error('Choose a file first.'));
      const { type, description: note } = resolved();
      return jobCandidateService.uploadDocument(candidateId, file, type, note);
    },
    onSuccess: async () => {
      await refresh();
      setUploading(null);
      setFile(null);
      setDescription('');
      toast({ title: 'Document uploaded' });
    },
    onError: (e: any) =>
      toast({ title: 'Upload refused', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (documentId: string) => jobCandidateService.deleteDocument(documentId),
    onSuccess: async () => {
      await refresh();
      setDeleting(null);
      toast({ title: 'Document removed' });
    },
    onError: (e: any) => toast({ title: 'Could not remove', description: e?.message, variant: 'destructive' }),
  });

  const download = async (doc: CandidateDocument) => {
    try {
      await hrDocumentService.download(
        `/job-candidates/${candidateId}/documents/${doc.id}/download`,
        doc.fileName,
      );
    } catch (e: any) {
      toast({ title: 'Could not download', description: e?.message, variant: 'destructive' });
    }
  };

  const rows = data ?? [];
  const idScans = rows.filter((d) => d.documentType === 'IdDocument');
  const letters = rows.filter((d) => d.documentType === 'ReferenceLetter');
  const generic = rows.filter((d) => d.documentType !== 'IdDocument' && d.documentType !== 'ReferenceLetter');

  const renderTable = (list: CandidateDocument[], emptyTitle: string, emptyDescription: string) =>
    list.length === 0 ? (
      <EmptyState icon={FileText} title={emptyTitle} description={emptyDescription} />
    ) : (
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>File</TableHead>
            <TableHead className="w-40">Type</TableHead>
            <TableHead>Description</TableHead>
            <TableHead className="w-36">Uploaded</TableHead>
            <TableHead className="w-32 text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {list.map((d) => (
            <TableRow key={d.id}>
              <TableCell className="font-medium">{d.fileName}</TableCell>
              <TableCell>{humanizeEnum(d.documentType)}</TableCell>
              <TableCell className="text-sm text-muted-foreground">{d.description || '—'}</TableCell>
              <TableCell className="text-sm text-muted-foreground">{formatDate(d.uploadDate)}</TableCell>
              <TableCell className="text-right">
                <div className="flex justify-end gap-0.5">
                  <Button variant="ghost" size="icon" onClick={() => download(d)} aria-label="Download">
                    <Download className="h-3.5 w-3.5" />
                  </Button>
                  {canEdit && (
                    <Button variant="ghost" size="icon" onClick={() => setDeleting(d)} aria-label="Remove">
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  )}
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    );

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-base">Documents</CardTitle>
          {canEdit && (
            <Button variant="outline" size="sm" onClick={() => open('generic')}>
              <Upload className="mr-2 h-4 w-4" />
              Upload document
            </Button>
          )}
        </CardHeader>
        <CardContent className="p-0">
          {renderTable(generic, 'No documents', 'CVs, transcripts and certificates land here, scanned and stored in the document repository.')}
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <div>
              <CardTitle className="text-base">Identity document</CardTitle>
              <p className="text-xs text-muted-foreground">
                {identityLabel ? `A scan of ${identityLabel}.` : 'No identity document is recorded on the profile yet.'}
              </p>
            </div>
            {canEdit && (
              <Button variant="outline" size="sm" onClick={() => open('id')}>
                <IdCard className="mr-2 h-4 w-4" />
                Attach scan
              </Button>
            )}
          </CardHeader>
          <CardContent className="p-0">
            {renderTable(idScans, 'No ID scan', 'Attach a scan of the identity document recorded on the profile.')}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <div>
              <CardTitle className="text-base">Reference letters</CardTitle>
              <p className="text-xs text-muted-foreground">Each letter is attached to the referee it comes from.</p>
            </div>
            {canEdit && (
              <Button variant="outline" size="sm" onClick={() => open('reference')} disabled={referees.length === 0}
                title={referees.length === 0 ? 'Add a referee first' : undefined}>
                <MailCheck className="mr-2 h-4 w-4" />
                Attach letter
              </Button>
            )}
          </CardHeader>
          <CardContent className="p-0">
            {renderTable(letters, 'No reference letters', referees.length === 0 ? 'Add a referee on the Referees tab, then attach their letter here.' : 'Attach a letter from one of the referees.')}
          </CardContent>
        </Card>
      </div>

      <Dialog open={!!uploading} onOpenChange={(o) => !o && setUploading(null)}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>
              {uploading === 'id' ? 'Attach the ID scan' : uploading === 'reference' ? 'Attach a reference letter' : 'Upload document'}
            </DialogTitle>
            <DialogDescription>
              The file is scanned and registered in the document repository before it is attached.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            {uploading === 'generic' && (
              <div className="space-y-1.5">
                <Label htmlFor="candidate-doc-type">Document type</Label>
                <Select value={documentType} onValueChange={(v) => setDocumentType(v as CandidateDocumentType)}>
                  <SelectTrigger id="candidate-doc-type">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CANDIDATE_GENERIC_DOCUMENT_TYPES.map((t) => (
                      <SelectItem key={t} value={t}>
                        {humanizeEnum(t)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            {uploading === 'reference' && (
              <div className="space-y-1.5">
                <Label htmlFor="candidate-doc-referee">From which referee</Label>
                <Select value={refereeId} onValueChange={setRefereeId}>
                  <SelectTrigger id="candidate-doc-referee">
                    <SelectValue placeholder="Choose the referee" />
                  </SelectTrigger>
                  <SelectContent>
                    {referees.map((r) => (
                      <SelectItem key={r.id} value={r.id}>
                        {r.fullName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            {uploading === 'id' && (
              <p className="text-sm text-muted-foreground">
                Stored as an ID document{identityLabel ? ` for ${identityLabel}` : ''}.
              </p>
            )}
            <div className="space-y-1.5">
              <Label htmlFor="candidate-doc-file">File</Label>
              <Input id="candidate-doc-file" type="file" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
            </div>
            {uploading === 'generic' && (
              <div className="space-y-1.5">
                <Label htmlFor="candidate-doc-note">What it is</Label>
                <Input
                  id="candidate-doc-note"
                  value={description}
                  maxLength={500}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Optional — e.g. Final-year transcript"
                />
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setUploading(null)}>
              Cancel
            </Button>
            <Button disabled={!file || upload.isPending || (uploading === 'reference' && !refereeId)} onClick={() => upload.mutate()}>
              {upload.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Upload
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={!!deleting}
        onOpenChange={(o) => !o && setDeleting(null)}
        title={`Remove "${deleting?.fileName}"?`}
        description="The document is detached from this candidate."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (deleting) await remove.mutateAsync(deleting.id);
          return true;
        }}
      />
    </div>
  );
}
