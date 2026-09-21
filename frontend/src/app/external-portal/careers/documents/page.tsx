'use client';

import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, FileText, Loader2, Trash2, Upload } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { candidateService } from '@/services/hr/careers.service';

// JobCandidateDocumentType — HREnums.cs (Resume=1 … Other=9), source-verified: a first draft
// guessed "CV" and "Reference", neither of which exists.
//
// ⚠ ReferenceLetter and IdDocument are deliberately NOT offered here (round 3, lane C2; decision
// D-17): a reference letter is attached from the referee it vouches for and an ID scan from the
// identity document, both on the profile page, so each upload already knows what it is.
const DOCUMENT_TYPES = [
  'Resume',
  'CoverLetter',
  'Transcript',
  'Certificate',
  'License',
  'Portfolio',
  'Other',
] as const;

/**
 * The candidate's own files — uploaded through the controlled (scanned) gate, downloadable only
 * with their bearer token, and deletable while a recruitment process has not consumed them.
 */
export default function CandidateDocumentsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const fileInput = useRef<HTMLInputElement>(null);
  // ⚠ Must be a member of the enum above. This defaulted to 'CV' — not a member — so the very
  // first upload was refused with 400 unless the dropdown had been touched (round 3, lane C2).
  const [documentType, setDocumentType] = useState<string>('Resume');
  const [description, setDescription] = useState('');

  const documents = useQuery({
    queryKey: ['candidate', 'documents'],
    queryFn: () => candidateService.getDocuments(),
  });

  const upload = useMutation({
    mutationFn: (file: File) => candidateService.uploadDocument(file, documentType, description),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['candidate', 'documents'] });
      setDescription('');
      toast({ title: 'Document uploaded' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not upload', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => candidateService.deleteDocument(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['candidate', 'documents'] });
      toast({ title: 'Document removed' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not remove it', description: e?.message, variant: 'destructive' }),
  });

  const download = async (id: string, fileName: string) => {
    try {
      const blob = await candidateService.downloadDocument(id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e: any) {
      toast({ title: 'Could not download', description: e?.message, variant: 'destructive' });
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-semibold">My documents</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          CVs, certificates and anything else you want on file with your applications. Reference
          letters and your ID scan are attached on your profile, beside the referee and the identity
          document they belong to.
        </p>
      </div>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Upload</CardTitle>
          <CardDescription>Files are virus-scanned before they are stored.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-wrap items-end gap-3">
          <div className="space-y-1.5">
            <Label>Document type</Label>
            <Select value={documentType} onValueChange={setDocumentType}>
              <SelectTrigger className="w-44">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {DOCUMENT_TYPES.map((t) => (
                  <SelectItem key={t} value={t}>
                    {humanizeEnum(t)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="min-w-[240px] flex-1 space-y-1.5">
            <Label htmlFor="doc-description">What it is (optional)</Label>
            <Input
              id="doc-description"
              value={description}
              maxLength={500}
              placeholder="e.g. Final-year transcript, University of Ghana"
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>
          <input
            ref={fileInput}
            type="file"
            hidden
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) upload.mutate(file);
              e.target.value = '';
            }}
          />
          <Button disabled={upload.isPending} onClick={() => fileInput.current?.click()}>
            {upload.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Upload className="mr-2 h-4 w-4" />
            )}
            Choose file
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {documents.isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : (documents.data ?? []).length === 0 ? (
            <div className="py-12 text-center text-muted-foreground">
              <FileText className="mx-auto mb-3 h-8 w-8" />
              No documents yet.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>File</TableHead>
                  <TableHead className="w-36">Type</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead className="w-32">Uploaded</TableHead>
                  <TableHead className="w-28 text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(documents.data ?? []).map((d) => (
                  <TableRow key={d.id}>
                    <TableCell className="font-medium">{d.fileName}</TableCell>
                    <TableCell>
                      <Badge variant="secondary">{humanizeEnum(d.documentType)}</Badge>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">{d.description || '—'}</TableCell>
                    <TableCell className="text-sm">{formatDate(d.uploadDate)}</TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-0.5">
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Download ${d.fileName}`}
                          onClick={() => download(d.id, d.fileName)}
                        >
                          <Download className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Delete ${d.fileName}`}
                          onClick={() => remove.mutate(d.id)}
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
