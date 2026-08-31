'use client';

import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, FileText, Loader2, Trash2, Upload } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
const DOCUMENT_TYPES = [
  'Resume',
  'CoverLetter',
  'Transcript',
  'Certificate',
  'License',
  'Portfolio',
  'ReferenceLetter',
  'IdDocument',
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
  const [documentType, setDocumentType] = useState<string>('CV');

  const documents = useQuery({
    queryKey: ['candidate', 'documents'],
    queryFn: () => candidateService.getDocuments(),
  });

  const upload = useMutation({
    mutationFn: (file: File) => candidateService.uploadDocument(file, documentType),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['candidate', 'documents'] });
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
          CVs, certificates and anything else you want on file with your applications.
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
