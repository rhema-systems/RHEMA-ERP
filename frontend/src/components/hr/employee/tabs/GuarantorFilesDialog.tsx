'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Loader2, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { HR_ADMIN_ROLES } from '@/components/hr/common/PermissionGate';
import { PhotoPanel } from '@/components/hr/common/PhotoDialog';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { employeeDocumentService } from '@/services/hr/employee-document.service';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import type { EmployeeGuarantor } from '@/types/hr/employee-subresources';
import type { EmployeeGuarantorDocument } from '@/types/hr/employee-documents';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtSize = (b?: number | null) =>
  b == null ? '—' : b < 1024 ? `${b} B` : b < 1048576 ? `${(b / 1024).toFixed(1)} KB` : `${(b / 1048576).toFixed(1)} MB`;

/**
 * The guarantor's photograph and papers (demo feedback round 2, E-12: "upload picture of the
 * guarantor and any documents pertaining to the guarantor").
 *
 * One face, many papers: the photo is six columns on the guarantor row and replaces itself; the
 * documents are a collection with a kind each, speaking the employee-document vocabulary. Both go
 * through the gate — the legacy `guarantorFormPath` text box this replaces is no longer writable.
 */
export function GuarantorFilesDialog({
  employeeId,
  guarantor,
  open,
  onOpenChange,
}: {
  employeeId: string;
  guarantor: EmployeeGuarantor | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const qc = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission, hasAnyRole } = useAuth();
  // The server gates the delete on EmployeeAdmin, a higher bar than filing; the affordance matches.
  const canDelete = hasAnyPermission(['HR.Employee.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const guarantorId = guarantor?.id ?? '';
  const name = guarantor ? [guarantor.firstName, guarantor.lastName].filter(Boolean).join(' ') : '';

  const [file, setFile] = useState<File | null>(null);
  const [form, setForm] = useState({ documentTypeId: '', title: '', issuedOn: '', expiresOn: '' });
  const [removing, setRemoving] = useState<EmployeeGuarantorDocument | null>(null);

  const { data: documents, isLoading } = useQuery({
    queryKey: ['hr', 'guarantor-documents', guarantorId],
    queryFn: () => employeeDocumentService.getGuarantorDocuments(guarantorId),
    enabled: open && !!guarantorId,
  });

  const { data: types } = useQuery({
    queryKey: ['employee-document-types'],
    queryFn: () => employeeDocumentService.getTypes(),
    enabled: open,
  });
  const selectedType = types?.find((t) => t.id === form.documentTypeId);

  // The guarantor list carries hasPhoto and documentCount, so both must refresh with this dialog.
  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ['hr', 'guarantor-documents', guarantorId] });
    void qc.invalidateQueries({ queryKey: ['hr', 'employees', employeeId, 'guarantors'] });
  };

  const reset = () => {
    setFile(null);
    setForm({ documentTypeId: '', title: '', issuedOn: '', expiresOn: '' });
  };

  const upload = useMutation({
    mutationFn: () => {
      if (!file) throw new Error('Choose a file first.');
      if (!form.documentTypeId) throw new Error('Choose what kind of document this is.');
      return employeeDocumentService.uploadGuarantorDocument(guarantorId, file, {
        documentTypeId: form.documentTypeId,
        title: form.title || null,
        issuedOn: form.issuedOn || null,
        expiresOn: form.expiresOn || null,
      });
    },
    onSuccess: () => { toast({ title: 'Document filed' }); reset(); refresh(); },
    onError: (e: unknown) => toast({
      variant: 'destructive',
      title: 'Not filed',
      description: e instanceof Error ? e.message : 'The upload was refused.',
    }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => employeeDocumentService.deleteGuarantorDocument(id),
    onSuccess: () => { toast({ title: 'Document removed' }); setRemoving(null); refresh(); },
    onError: (e: unknown) => toast({
      variant: 'destructive', title: 'Not removed',
      description: e instanceof Error ? e.message : 'Refused',
    }),
  });

  const download = async (d: EmployeeGuarantorDocument) => {
    try {
      await hrDocumentService.download(
        employeeDocumentService.guarantorDocumentDownloadUrl(d.id), d.fileName ?? 'document');
    } catch {
      toast({ variant: 'destructive', title: 'Could not download that file' });
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => { onOpenChange(o); if (!o) reset(); }}>
      <DialogContent className="sm:max-w-[820px]">
        <DialogHeader>
          <DialogTitle>Files — {name}</DialogTitle>
          <DialogDescription>
            The guarantor's photograph and any documents pertaining to them: the signed guarantor
            form, an ID scan, a payslip. Every file is virus-scanned and registered in the document
            store.
          </DialogDescription>
        </DialogHeader>

        {guarantor && (
          <div className="space-y-6">
            <section className="space-y-2">
              <h4 className="text-sm font-semibold">Photograph</h4>
              <PhotoPanel
                endpoint={employeeDocumentService.guarantorPhotoUrl(guarantor.id)}
                hasPhoto={!!guarantor.hasPhoto}
                upload={(f) => employeeDocumentService.uploadGuarantorPhoto(guarantor.id, f)}
                onUploaded={refresh}
                subjectLabel={name}
              />
            </section>

            <section className="space-y-3">
              <h4 className="text-sm font-semibold">Documents</h4>
              {isLoading ? (
                <div className="flex justify-center py-6">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : (documents ?? []).length === 0 ? (
                <p className="text-sm text-muted-foreground">No documents on file for this guarantor.</p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Kind</TableHead>
                      <TableHead>Title</TableHead>
                      <TableHead>Issued</TableHead>
                      <TableHead>Expires</TableHead>
                      <TableHead>Size</TableHead>
                      <TableHead>Filed by</TableHead>
                      <TableHead className="w-24" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(documents ?? []).map((d) => (
                      <TableRow key={d.id}>
                        <TableCell>{d.documentTypeName ?? '—'}</TableCell>
                        <TableCell>{d.title ?? d.fileName ?? '—'}</TableCell>
                        <TableCell>{fmtDate(d.issuedOn)}</TableCell>
                        <TableCell>
                          <span className="inline-flex items-center gap-2">
                            {fmtDate(d.expiresOn)}
                            {d.isExpired && <Badge variant="destructive">Expired</Badge>}
                          </span>
                        </TableCell>
                        <TableCell>{fmtSize(d.fileSizeBytes)}</TableCell>
                        <TableCell>{d.uploadedByName ?? '—'}</TableCell>
                        <TableCell>
                          <div className="flex justify-end gap-1">
                            <Button variant="ghost" size="icon" aria-label="Download" onClick={() => download(d)}>
                              <Download className="h-4 w-4" />
                            </Button>
                            {canDelete && (
                              <Button variant="ghost" size="icon" aria-label="Remove" onClick={() => setRemoving(d)}>
                                <Trash2 className="h-4 w-4 text-destructive" />
                              </Button>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}

              <div className="rounded-md border p-3">
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <div className="space-y-2">
                    <Label>What kind of document</Label>
                    <Select
                      value={form.documentTypeId}
                      onValueChange={(v) => setForm((f) => ({ ...f, documentTypeId: v }))}
                    >
                      <SelectTrigger><SelectValue placeholder="Choose a kind…" /></SelectTrigger>
                      <SelectContent>
                        {(types ?? []).filter((t) => t.isActive).map((t) => (
                          <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Title</Label>
                    <Input
                      value={form.title}
                      placeholder="Optional"
                      onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Issued on</Label>
                    <Input
                      type="date"
                      value={form.issuedOn}
                      onChange={(e) => setForm((f) => ({ ...f, issuedOn: e.target.value }))}
                    />
                  </div>
                  {/* Whether to ask for an expiry is a property of the KIND, decided once on the
                      vocabulary rather than guessed per document. */}
                  {selectedType?.hasExpiry && (
                    <div className="space-y-2">
                      <Label>Expires on</Label>
                      <Input
                        type="date"
                        value={form.expiresOn}
                        onChange={(e) => setForm((f) => ({ ...f, expiresOn: e.target.value }))}
                      />
                    </div>
                  )}
                  <div className="space-y-2 sm:col-span-2">
                    <Label>File</Label>
                    <Input type="file" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
                  </div>
                </div>
                <div className="mt-3 flex justify-end">
                  <Button
                    size="sm"
                    onClick={() => upload.mutate()}
                    disabled={upload.isPending || !file || !form.documentTypeId}
                  >
                    {upload.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    File document
                  </Button>
                </div>
              </div>
            </section>
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Close</Button>
        </DialogFooter>

        <ConfirmationDialog
          open={removing !== null}
          onOpenChange={(o) => !o && setRemoving(null)}
          title="Remove this document?"
          description="It stays in the document store's history but leaves the guarantor's file."
          confirmText="Remove"
          variant="destructive"
          onConfirm={() => { if (removing) remove.mutate(removing.id); }}
        />
      </DialogContent>
    </Dialog>
  );
}
