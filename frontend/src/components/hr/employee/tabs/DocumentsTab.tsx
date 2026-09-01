'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle, CheckCircle2, Download, FileText, Loader2, Pencil, Trash2, Upload,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { HR_ADMIN_ROLES, HR_ROLES } from '@/components/hr/common/PermissionGate';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { employeeDocumentService } from '@/services/hr/employee-document.service';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import type { EmployeeDocument } from '@/types/hr/employee-documents';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtSize = (b?: number | null) =>
  b == null ? '—' : b < 1024 ? `${b} B` : b < 1048576 ? `${(b / 1024).toFixed(1)} KB` : `${(b / 1048576).toFixed(1)} MB`;

/**
 * The employee's document file, and whether it satisfies what their position requires.
 *
 * ⚠ **This tab is the reason lane 3c existed.** The controlled upload gate is wired into thirty-odd
 * HR controllers and the one entity they all hang off could hold no file at all — the most-used
 * record in the module could not carry a signed contract, an ID scan or a certificate.
 *
 * ⚠ **Compliance is read from the server, never derived here.** It is also the only read that
 * resolves this employee's position: the paged employee list does not return one.
 */
export function DocumentsTab({ employeeId }: { employeeId: string }) {
  const { toast } = useToast();
  const qc = useQueryClient();
  const { hasAnyPermission, hasAnyRole } = useAuth();

  const canWrite = hasAnyPermission(['HR.Employee.Write', 'HR.Employee.Admin']) || hasAnyRole(HR_ROLES);
  // The server gates the delete on EmployeeAdmin — a strictly higher bar than filing. Gating the
  // affordance to match is the point: a desk offered a button that 403s learns nothing from it.
  const canDelete = hasAnyPermission(['HR.Employee.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const [uploadOpen, setUploadOpen] = useState(false);
  const [editing, setEditing] = useState<EmployeeDocument | null>(null);
  const [removing, setRemoving] = useState<EmployeeDocument | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [form, setForm] = useState({
    documentTypeId: '', title: '', description: '', issuedOn: '', expiresOn: '',
  });

  const { data: documents, isLoading } = useQuery({
    queryKey: ['employee-documents', employeeId],
    queryFn: () => employeeDocumentService.getForEmployee(employeeId),
  });

  const { data: types } = useQuery({
    queryKey: ['employee-document-types'],
    queryFn: () => employeeDocumentService.getTypes(),
  });

  const { data: compliance } = useQuery({
    queryKey: ['employee-document-compliance', employeeId],
    queryFn: () => employeeDocumentService.getCompliance(employeeId),
  });

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ['employee-documents', employeeId] });
    void qc.invalidateQueries({ queryKey: ['employee-document-compliance', employeeId] });
    void qc.invalidateQueries({ queryKey: ['employee-document-types'] });
  };

  const selectedType = types?.find((t) => t.id === form.documentTypeId);

  const reset = () => {
    setFile(null);
    setForm({ documentTypeId: '', title: '', description: '', issuedOn: '', expiresOn: '' });
  };

  const upload = useMutation({
    mutationFn: () => {
      if (!file) throw new Error('Choose a file first.');
      if (!form.documentTypeId) throw new Error('Choose what kind of document this is.');
      return employeeDocumentService.upload(employeeId, file, {
        documentTypeId: form.documentTypeId,
        title: form.title || null,
        description: form.description || null,
        issuedOn: form.issuedOn || null,
        expiresOn: form.expiresOn || null,
      });
    },
    onSuccess: () => {
      toast({ title: 'Document filed' });
      setUploadOpen(false);
      reset();
      refresh();
    },
    onError: (e: unknown) => toast({
      variant: 'destructive',
      title: 'Not filed',
      description: e instanceof Error ? e.message : 'The upload was refused.',
    }),
  });

  const save = useMutation({
    mutationFn: (documentId: string) => employeeDocumentService.update(documentId, {
      documentTypeId: form.documentTypeId,
      title: form.title || null,
      description: form.description || null,
      issuedOn: form.issuedOn || null,
      expiresOn: form.expiresOn || null,
    }),
    onSuccess: () => { toast({ title: 'Document updated' }); setEditing(null); reset(); refresh(); },
    onError: (e: unknown) => toast({
      variant: 'destructive', title: 'Not saved',
      description: e instanceof Error ? e.message : 'Refused',
    }),
  });

  const remove = useMutation({
    mutationFn: (documentId: string) => employeeDocumentService.delete(documentId),
    onSuccess: () => { toast({ title: 'Document removed' }); setRemoving(null); refresh(); },
    onError: (e: unknown) => toast({
      variant: 'destructive', title: 'Not removed',
      description: e instanceof Error ? e.message : 'Refused',
    }),
  });

  const download = async (d: EmployeeDocument) => {
    try {
      // ⚠ Never an <a href>: the file sits outside the web root and the request needs the bearer
      // token. This is the same helper every other gated HR download uses.
      await hrDocumentService.download(
        employeeDocumentService.downloadUrl(d.id), d.fileName ?? 'document');
    } catch {
      toast({ variant: 'destructive', title: 'Could not download that file' });
    }
  };

  const openEdit = (d: EmployeeDocument) => {
    setEditing(d);
    setForm({
      documentTypeId: d.documentTypeId,
      title: d.title ?? '',
      description: d.description ?? '',
      issuedOn: d.issuedOn ?? '',
      expiresOn: d.expiresOn ?? '',
    });
  };

  const metadataFields = (
    <div className="space-y-4">
      <div className="space-y-2">
        <Label>What kind of document</Label>
        <Select
          value={form.documentTypeId}
          onValueChange={(v) => setForm((f) => ({ ...f, documentTypeId: v }))}
        >
          <SelectTrigger><SelectValue placeholder="Choose a document type" /></SelectTrigger>
          <SelectContent>
            {(types ?? []).map((t) => (
              <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="space-y-2">
        <Label>Title</Label>
        <Input
          value={form.title}
          placeholder="Optional — how this copy should be listed"
          onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
        />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-2">
          <Label>Issued on</Label>
          <Input
            type="date" value={form.issuedOn}
            onChange={(e) => setForm((f) => ({ ...f, issuedOn: e.target.value }))}
          />
        </div>
        {/* The type decides whether an expiry is asked for at all — a passport expires and a
            signed contract does not. */}
        {selectedType?.hasExpiry && (
          <div className="space-y-2">
            <Label>Expires on</Label>
            <Input
              type="date" value={form.expiresOn}
              onChange={(e) => setForm((f) => ({ ...f, expiresOn: e.target.value }))}
            />
          </div>
        )}
      </div>
      <div className="space-y-2">
        <Label>Notes</Label>
        <Textarea
          rows={2} value={form.description}
          onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
        />
      </div>
    </div>
  );

  return (
    <div className="space-y-4">
      {/* ── What the position requires ──────────────────────────────────────── */}
      {compliance && compliance.lines.length > 0 && (
        <Card className={compliance.isCompliant ? undefined : 'border-amber-400'}>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              {compliance.isCompliant
                ? <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                : <AlertTriangle className="h-4 w-4 text-amber-600" />}
              Required for {compliance.positionTitle ?? 'this position'}
            </CardTitle>
            <CardDescription>
              {compliance.mandatorySatisfiedCount} of {compliance.mandatoryCount} mandatory
              document{compliance.mandatoryCount === 1 ? '' : 's'} on file.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {compliance.lines.map((line) => (
              <Badge
                key={line.documentTypeId}
                variant="secondary"
                className={
                  line.isSatisfied
                    ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200'
                    : line.isExpiredOnly
                      ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200'
                      : 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200'
                }
              >
                {line.documentTypeName}
                {/* ⚠ Three states, not two. "Lapsed" and "never filed" are different conversations
                    and the server distinguishes them; collapsing them here would throw that away. */}
                {line.isSatisfied
                  ? line.daysUntilExpiry != null && line.daysUntilExpiry < 60
                    ? ` · expires in ${line.daysUntilExpiry}d`
                    : ' · on file'
                  : line.isExpiredOnly ? ' · lapsed' : ' · not filed'}
                {!line.isMandatory && ' · optional'}
              </Badge>
            ))}
          </CardContent>
        </Card>
      )}

      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          {documents?.length ?? 0} document{documents?.length === 1 ? '' : 's'} on file.
        </p>
        {canWrite && (
          <Button onClick={() => { reset(); setUploadOpen(true); }}>
            <Upload className="mr-2 h-4 w-4" />
            File a document
          </Button>
        )}
      </div>

      {isLoading ? (
        <div className="flex justify-center p-8">
          <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
        </div>
      ) : !documents || documents.length === 0 ? (
        <EmptyState
          icon={FileText}
          title="Nothing on file"
          description="Contracts, ID scans and certificates filed here go through the same virus-scanned upload gate as the rest of HR."
        />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Document</TableHead>
              <TableHead>Kind</TableHead>
              <TableHead>Issued</TableHead>
              <TableHead>Expires</TableHead>
              <TableHead>Filed by</TableHead>
              <TableHead className="w-[120px]" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {documents.map((d) => (
              <TableRow key={d.id}>
                <TableCell>
                  <p className="font-medium">{d.title || d.fileName || 'Untitled'}</p>
                  <p className="text-xs text-muted-foreground">
                    {d.fileName} · {fmtSize(d.fileSizeBytes)}
                  </p>
                </TableCell>
                <TableCell>{d.documentTypeName ?? '—'}</TableCell>
                <TableCell>{fmtDate(d.issuedOn)}</TableCell>
                <TableCell>
                  {d.expiresOn == null ? (
                    <span className="text-muted-foreground">—</span>
                  ) : d.isExpired ? (
                    <Badge variant="destructive">Expired {fmtDate(d.expiresOn)}</Badge>
                  ) : d.daysUntilExpiry != null && d.daysUntilExpiry < 60 ? (
                    <Badge className="bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200" variant="secondary">
                      {d.daysUntilExpiry}d left
                    </Badge>
                  ) : (
                    fmtDate(d.expiresOn)
                  )}
                </TableCell>
                <TableCell className="text-sm text-muted-foreground">
                  {d.uploadedByName ?? '—'}
                  <br />
                  <span className="text-xs">{fmtDate(d.createdAt)}</span>
                </TableCell>
                <TableCell>
                  <div className="flex justify-end gap-1">
                    <Button variant="ghost" size="icon" aria-label="Download" onClick={() => download(d)}>
                      <Download className="h-4 w-4" />
                    </Button>
                    {canWrite && (
                      <Button variant="ghost" size="icon" aria-label="Edit" onClick={() => openEdit(d)}>
                        <Pencil className="h-4 w-4" />
                      </Button>
                    )}
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

      {/* ── File a document ─────────────────────────────────────────────────── */}
      <Dialog open={uploadOpen} onOpenChange={(o) => { setUploadOpen(o); if (!o) reset(); }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>File a document</DialogTitle>
            <DialogDescription>
              The file is virus-scanned and registered in the document store. Whoever files it is
              recorded automatically.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>File</Label>
              <Input type="file" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
            </div>
            {metadataFields}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setUploadOpen(false)}>Cancel</Button>
            <Button
              onClick={() => upload.mutate()}
              disabled={upload.isPending || !file || !form.documentTypeId}
            >
              {upload.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              File it
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Correct the metadata ────────────────────────────────────────────── */}
      <Dialog open={editing !== null} onOpenChange={(o) => { if (!o) { setEditing(null); reset(); } }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Correct this document</DialogTitle>
            <DialogDescription>
              Changes what the record SAYS. To replace the file itself, file a new one.
            </DialogDescription>
          </DialogHeader>
          {metadataFields}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)}>Cancel</Button>
            <Button
              onClick={() => editing && save.mutate(editing.id)}
              disabled={save.isPending || !form.documentTypeId}
            >
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AlertDialog open={removing !== null} onOpenChange={(o) => !o && setRemoving(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              Remove {removing?.title || removing?.fileName || 'this document'}?
            </AlertDialogTitle>
            <AlertDialogDescription>
              It leaves the employee&apos;s file and stops counting towards anything their position
              requires.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => removing && remove.mutate(removing.id)}
              disabled={remove.isPending}
            >
              {remove.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Remove
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
