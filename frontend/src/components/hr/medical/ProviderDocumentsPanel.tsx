'use client';

import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Download, FileWarning, Loader2, Paperclip, Trash2, Upload } from 'lucide-react';
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
import { medicalInsuranceService } from '@/services/hr/medical-reference.service';
import {
  PROVIDER_DOCUMENT_TYPE_OPTIONS,
  type MedicalInsuranceProviderDocument,
  type MedicalInsuranceProviderDocumentType,
} from '@/types/hr/medical';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const MAX_BYTES = 10 * 1024 * 1024;
const fmtSize = (b: number) =>
  b >= 1_048_576 ? `${(b / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(b / 1024))} KB`;

const labelFor = (v?: MedicalInsuranceProviderDocumentType | null) =>
  PROVIDER_DOCUMENT_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? (v ?? '—');

/** Expiring within 60 days — a lapsed operating licence is the thing this panel exists to catch. */
const expiringSoon = (v?: string | null) => {
  if (!v) return false;
  const days = (new Date(v).getTime() - Date.now()) / 86_400_000;
  return days >= 0 && days <= 60;
};
const expired = (v?: string | null) => !!v && new Date(v).getTime() < Date.now();

/**
 * The provider's own paperwork — operating licence, rate card, contract, solvency certificate.
 *
 * ⚠ **This collection could not be created safely until the upload endpoint existed.** The only
 * create route took a caller-supplied `FilePath`, which let anyone point a document row at
 * arbitrary bytes on disk — the third instance of that defect in the medical module, after
 * `MedicalExpenseDocument` and `EmployeeMedicalExamDocument` were both fixed for it. There was no
 * download route either, so the row was unreadable even when the path was honest. Files now go
 * through the scanning + DMS gate and come back through a token-bearing download.
 *
 * ⚠ **`filePath` is a legacy server-side location, never a URL.** It stays empty on anything
 * uploaded through the gate. A row that still carries one predates the fix and has no scanned
 * file behind it — the panel says so rather than offering a download that cannot work.
 *
 * ⚠ **Expiry is the point of the collection.** An insurer whose operating licence has lapsed is
 * not an insurer, so expiry is surfaced on the row rather than buried in a detail dialog.
 */
export function ProviderDocumentsPanel({
  providerId,
  canWrite,
  canDelete,
}: {
  providerId: string;
  canWrite: boolean;
  canDelete: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const fileInput = useRef<HTMLInputElement>(null);

  const queryKey = ['hr', 'medical-providers', providerId, 'documents'];
  const { data: documents, isLoading } = useQuery({
    queryKey,
    queryFn: () => medicalInsuranceService.getProviderDocuments(providerId),
    enabled: !!providerId,
  });

  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [documentType, setDocumentType] = useState<MedicalInsuranceProviderDocumentType>('OperatingLicense');
  const [description, setDescription] = useState('');
  const [expiryDate, setExpiryDate] = useState('');
  const [pendingDelete, setPendingDelete] = useState<MedicalInsuranceProviderDocument | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);

  const reset = () => {
    setFile(null);
    setDocumentType('OperatingLicense');
    setDescription('');
    setExpiryDate('');
    if (fileInput.current) fileInput.current.value = '';
  };

  const fail = (title: string) => (e: any) =>
    toast({
      title,
      description: e?.response?.data?.message ?? e?.response?.data?.detail ?? e?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const upload = useMutation({
    mutationFn: () =>
      medicalInsuranceService.uploadProviderDocument(file as File, {
        providerId,
        documentType,
        description: description.trim() || null,
        expiryDate: expiryDate || null,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey });
      toast({ title: 'Document attached' });
      setOpen(false);
      reset();
    },
    onError: fail('Could not attach the document'),
  });

  const remove = useMutation({
    mutationFn: (doc: MedicalInsuranceProviderDocument) =>
      medicalInsuranceService.removeProviderDocument(doc.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey });
      toast({ title: 'Document removed' });
      setPendingDelete(null);
    },
    onError: fail('Could not remove the document'),
  });

  const download = async (doc: MedicalInsuranceProviderDocument) => {
    setDownloading(doc.id);
    try {
      await medicalInsuranceService.downloadProviderDocument(doc.id, doc.fileName);
    } catch (e) {
      fail('Could not download the document')(e);
    } finally {
      setDownloading(null);
    }
  };

  const tooBig = !!file && file.size > MAX_BYTES;
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
              description="Nothing is held against this provider — not its operating licence, contract or rate card. Files are scanned and stored privately."
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Document</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Uploaded</TableHead>
                <TableHead>Expires</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((d) => {
                // No scanned file behind it — a row that predates the upload gate.
                const legacy = !d.fileUploadRecordId && !d.documentRecordId;
                return (
                  <TableRow key={d.id}>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        {d.fileName}
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
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline">{d.documentTypeName ?? labelFor(d.documentType)}</Badge>
                    </TableCell>
                    <TableCell>{fmtDate(d.uploadDate)}</TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        {fmtDate(d.expiryDate)}
                        {expired(d.expiryDate) && (
                          <Badge variant="destructive" className="gap-1">
                            <AlertTriangle className="h-3 w-3" />
                            Expired
                          </Badge>
                        )}
                        {!expired(d.expiryDate) && expiringSoon(d.expiryDate) && (
                          <Badge className="bg-amber-100 text-amber-800">Expiring</Badge>
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => download(d)}
                          // A legacy row has no scanned file to stream.
                          disabled={downloading === d.id || legacy}
                          title={legacy ? 'This row predates the upload gate and has no stored file.' : undefined}
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
            <DialogTitle>Attach a provider document</DialogTitle>
            <DialogDescription>
              The file is scanned and stored privately. It can only be read back through this screen.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="pd-file">File<span className="ml-0.5 text-red-500">*</span></Label>
              <Input
                id="pd-file"
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
                <Label htmlFor="pd-type">Type</Label>
                <Select
                  value={documentType}
                  onValueChange={(v) => setDocumentType(v as MedicalInsuranceProviderDocumentType)}
                >
                  <SelectTrigger id="pd-type"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {PROVIDER_DOCUMENT_TYPE_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="pd-expiry">Expires</Label>
                <Input
                  id="pd-expiry"
                  type="date"
                  value={expiryDate}
                  onChange={(e) => setExpiryDate(e.target.value)}
                />
                <p className="text-xs text-muted-foreground">
                  Licences and certificates lapse — dating them is how that gets noticed.
                </p>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="pd-description">Description</Label>
              <Input
                id="pd-description"
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
            <Button onClick={() => upload.mutate()} disabled={!file || tooBig || upload.isPending}>
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
        description={`"${pendingDelete?.fileName ?? ''}" will be removed from the provider.`}
        confirmText="Remove"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={async () => { if (pendingDelete) await remove.mutateAsync(pendingDelete); }}
      />
    </Card>
  );
}
