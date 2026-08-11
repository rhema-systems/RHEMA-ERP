'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, FileText, Loader2, Trash2, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
  CANDIDATE_DOCUMENT_TYPES,
  type CandidateDocument,
  type CandidateDocumentType,
} from '@/types/hr/recruitment-pipeline';

/**
 * Candidate documents — CVs, transcripts, certificates.
 *
 * ⚠ Uploads are **multipart through the controlled-upload gate**: the file is virus-scanned and
 * registered in the central DMS before the row is written. The old JSON endpoint took a `filePath`
 * from the caller, stored nothing, and wrote rows that could never be downloaded.
 *
 * ⚠ On a machine with no ClamAV the gate correctly refuses with 422 before anything is written, so a
 * local upload failure here is usually an environment problem, not a defect.
 *
 * Downloads are always blob fetches with the bearer token — the stored path is not a URL, and files
 * live outside the web root.
 */
export function CandidateDocumentsPanel({
  candidateId,
  canEdit,
}: {
  candidateId: string;
  canEdit: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [uploading, setUploading] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [documentType, setDocumentType] = useState<CandidateDocumentType>('Resume');
  const [description, setDescription] = useState('');
  const [deleting, setDeleting] = useState<CandidateDocument | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'candidate-documents', candidateId],
    queryFn: () => jobCandidateService.getDocuments(candidateId),
    enabled: !!candidateId,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'candidate-documents', candidateId] });

  const upload = useMutation({
    mutationFn: () =>
      file
        ? jobCandidateService.uploadDocument(candidateId, file, documentType, description.trim() || null)
        : Promise.reject(new Error('Choose a file first.')),
    onSuccess: async () => {
      await refresh();
      setUploading(false);
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

  return (
    <div className="space-y-4">
      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={FileText}
              title="No documents"
              description="Uploaded files are scanned and stored in the document repository."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>File</TableHead>
                  <TableHead className="w-44">Type</TableHead>
                  <TableHead className="w-36">Uploaded</TableHead>
                  <TableHead className="w-32 text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((d) => (
                  <TableRow key={d.id}>
                    <TableCell className="font-medium">{d.fileName}</TableCell>
                    <TableCell>{humanizeEnum(d.documentType)}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {formatDate(d.uploadDate)}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-0.5">
                        <Button variant="ghost" size="icon" onClick={() => download(d)} aria-label="Download">
                          <Download className="h-3.5 w-3.5" />
                        </Button>
                        {canEdit && (
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setDeleting(d)}
                            aria-label="Remove"
                          >
                            <Trash2 className="h-3.5 w-3.5" />
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

      {canEdit && (
        <Button variant="outline" onClick={() => setUploading(true)}>
          <Upload className="mr-2 h-4 w-4" />
          Upload document
        </Button>
      )}

      <Dialog open={uploading} onOpenChange={(o) => !o && setUploading(false)}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>Upload document</DialogTitle>
            <DialogDescription>
              The file is scanned and registered in the document repository before it is attached.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="candidate-doc-type">Document type</Label>
              <Select
                value={documentType}
                onValueChange={(v) => setDocumentType(v as CandidateDocumentType)}
              >
                <SelectTrigger id="candidate-doc-type">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {CANDIDATE_DOCUMENT_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {humanizeEnum(t)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="candidate-doc-file">File</Label>
              <Input
                id="candidate-doc-file"
                type="file"
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="candidate-doc-note">Note</Label>
              <Input
                id="candidate-doc-note"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Optional"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setUploading(false)}>
              Cancel
            </Button>
            <Button disabled={!file || upload.isPending} onClick={() => upload.mutate()}>
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
