'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Download, FileBadge, Loader2, MoreHorizontal, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyEnvironmentalComplianceService } from '@/services/hr/safety-environmental-compliance.service';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import { locationService } from '@/services/hr/location.service';
import { SHE_ENV_PERMIT_TYPE_OPTIONS } from '@/types/hr/safety-environment-compliance';
import type {
  SheEnvironmentalPermitSummary,
  SheEnvironmentalPermitType,
  SheEnvironmentalPermitStatus,
} from '@/types/hr/safety-environment-compliance';

/**
 * Environmental permit & licence register (FR-ENV-017–019): permits with their expiry-driven
 * renewal ladder (FR-ENV-018 reminders ride the reminder engine, not this screen), a guarded
 * renewal flow, and permit documents versioned on the central DMS through the controlled
 * upload gate — a permit with a document archives instead of deleting.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const isoDay = (d: Date) => d.toISOString().slice(0, 10);
const fmtSize = (bytes?: number | null) => {
  if (bytes == null) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

const permitSchema = z.object({
  permitName: z.string().min(1, 'A permit name is required').max(300),
  permitType: z.string().min(1),
  authorityReferenceNumber: z.string().max(100).optional().or(z.literal('')),
  issuingBodyId: z.string().optional().or(z.literal('')),
  responsibleOfficerId: z.string().min(1, 'A responsible officer is required'),
  locationId: z.string().optional().or(z.literal('')),
  // Create-only — the update request deliberately omits the dates (renewal owns them).
  issueDate: z.string().optional().or(z.literal('')),
  expiryDate: z.string().optional().or(z.literal('')),
  renewalPeriodMonths: z.coerce.number().int().positive().optional().or(z.literal('')),
  description: z.string().max(2000).optional().or(z.literal('')),
  conditions: z.string().max(4000).optional().or(z.literal('')),
  notes: z.string().max(2000).optional().or(z.literal('')),
});
type PermitForm = z.input<typeof permitSchema>;

const renewSchema = z.object({
  newIssueDate: z.string().min(1, 'The new issue date is required'),
  newExpiryDate: z.string().min(1, 'The new expiry date is required'),
  newAuthorityReferenceNumber: z.string().max(100).optional().or(z.literal('')),
  notes: z.string().max(2000).optional().or(z.literal('')),
});
type RenewForm = z.input<typeof renewSchema>;

function PermitStatusBadge({ status, name }: { status: SheEnvironmentalPermitStatus; name: string }) {
  switch (status) {
    case 'Active':
      return <Badge>{name}</Badge>;
    case 'RenewalInProgress':
      return <Badge variant="secondary">{name}</Badge>;
    case 'Expired':
      return <Badge variant="destructive">{name}</Badge>;
    case 'Suspended':
      return (
        <Badge variant="outline" className="border-amber-500 text-amber-600">
          {name}
        </Badge>
      );
    default:
      return (
        <Badge variant="outline" className="text-muted-foreground">
          {name}
        </Badge>
      );
  }
}

export default function EnvironmentalPermitsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<{ id: string; number: string } | null>(null);
  const [renewing, setRenewing] = useState<SheEnvironmentalPermitSummary | null>(null);
  const [pendingArchive, setPendingArchive] = useState<SheEnvironmentalPermitSummary | null>(null);
  const [pendingDelete, setPendingDelete] = useState<SheEnvironmentalPermitSummary | null>(null);
  const [documentsFor, setDocumentsFor] = useState<SheEnvironmentalPermitSummary | null>(null);
  const [uploadFile, setUploadFile] = useState<File | null>(null);
  const [uploadSummary, setUploadSummary] = useState('');
  const [busy, setBusy] = useState(false);

  const { data: all = [] } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'permits', 'all'],
    queryFn: () => safetyEnvironmentalComplianceService.getPermits(),
  });
  const { data: expiring = [] } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'permits', 'expiring', 90],
    queryFn: () => safetyEnvironmentalComplianceService.getPermits(undefined, undefined, undefined, 90),
  });
  const { data: expired = [] } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'permits', 'expired'],
    queryFn: () => safetyEnvironmentalComplianceService.getPermits('Expired'),
  });
  const { data: bodies = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'regulatory-bodies'],
    queryFn: () => safetyReferenceService.getRegulatoryBodies(true),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });
  const documentsForId = documentsFor?.id;
  const { data: documentPermit } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'permits', 'detail', documentsForId],
    queryFn: () => safetyEnvironmentalComplianceService.getPermit(documentsForId as string),
    enabled: documentsForId != null,
  });

  const permitForm = useForm<PermitForm>({ resolver: zodResolver(permitSchema) });
  const renewForm = useForm<RenewForm>({ resolver: zodResolver(renewSchema) });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-env-compliance'] });
  const fail = (fallback: string) => (error: any) =>
    toast({ title: 'Error', description: error?.message || fallback, variant: 'destructive' });

  const bodyOptions = bodies.map((b) => ({
    value: b.id,
    label: b.shortName ? `${b.shortName} — ${b.name}` : b.name,
  }));
  const locationOptions = locations.map((l) => ({ value: l.id, label: l.name }));

  // ── Create / edit ──

  const openCreate = () => {
    setEditing(null);
    permitForm.reset({
      permitName: '',
      permitType: 'EnvironmentalPermit',
      authorityReferenceNumber: '',
      issuingBodyId: '',
      responsibleOfficerId: '',
      locationId: '',
      issueDate: isoDay(new Date()),
      expiryDate: '',
      renewalPeriodMonths: '',
      description: '',
      conditions: '',
      notes: '',
    });
    setDialogOpen(true);
  };

  const openEdit = async (row: SheEnvironmentalPermitSummary) => {
    try {
      const full = await safetyEnvironmentalComplianceService.getPermit(row.id);
      permitForm.reset({
        permitName: full.permitName,
        permitType: full.permitType,
        authorityReferenceNumber: full.authorityReferenceNumber ?? '',
        issuingBodyId: full.issuingBodyId ?? '',
        responsibleOfficerId: full.responsibleOfficerId,
        locationId: full.locationId ?? '',
        issueDate: '',
        expiryDate: '',
        renewalPeriodMonths: full.renewalPeriodMonths ?? '',
        description: full.description ?? '',
        conditions: full.conditions ?? '',
        notes: full.notes ?? '',
      });
      setEditing({ id: full.id, number: full.registerNumber });
      setDialogOpen(true);
    } catch (error: any) {
      fail('Loading the permit failed.')(error);
    }
  };

  const submitPermit = permitForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = permitSchema.parse(values);
      const common = {
        permitName: v.permitName,
        permitType: v.permitType as SheEnvironmentalPermitType,
        authorityReferenceNumber: blank(v.authorityReferenceNumber),
        issuingBodyId: blank(v.issuingBodyId),
        responsibleOfficerId: v.responsibleOfficerId,
        locationId: blank(v.locationId),
        description: blank(v.description),
        conditions: blank(v.conditions),
        renewalPeriodMonths:
          v.renewalPeriodMonths === '' || v.renewalPeriodMonths == null
            ? null
            : v.renewalPeriodMonths,
        notes: blank(v.notes),
      };
      if (editing) {
        const saved = await safetyEnvironmentalComplianceService.updatePermit(editing.id, {
          id: editing.id,
          ...common,
        });
        await invalidate();
        toast({ title: 'Permit updated', description: saved.registerNumber });
      } else {
        if (!v.issueDate) {
          permitForm.setError('issueDate', { message: 'An issue date is required' });
          return;
        }
        if (!v.expiryDate) {
          permitForm.setError('expiryDate', { message: 'An expiry date is required' });
          return;
        }
        const saved = await safetyEnvironmentalComplianceService.createPermit({
          ...common,
          issueDate: new Date(v.issueDate).toISOString(),
          expiryDate: new Date(v.expiryDate).toISOString(),
        });
        await invalidate();
        toast({ title: 'Permit registered', description: saved.registerNumber });
      }
      setDialogOpen(false);
    } catch (error: any) {
      fail('Saving the permit failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  // ── Lifecycle actions ──

  const markRenewal = async (row: SheEnvironmentalPermitSummary) => {
    setBusy(true);
    try {
      await safetyEnvironmentalComplianceService.markPermitRenewal(row.id);
      await invalidate();
      toast({ title: 'Renewal in progress', description: row.registerNumber });
    } catch (error: any) {
      fail('Marking the renewal failed.')(error);
    } finally {
      setBusy(false);
    }
  };

  const suspend = async (row: SheEnvironmentalPermitSummary) => {
    setBusy(true);
    try {
      await safetyEnvironmentalComplianceService.suspendPermit(row.id);
      await invalidate();
      toast({ title: 'Permit suspended', description: row.registerNumber });
    } catch (error: any) {
      fail('Suspending the permit failed.')(error);
    } finally {
      setBusy(false);
    }
  };

  const submitRenew = renewForm.handleSubmit(async (values) => {
    if (!renewing) return;
    setBusy(true);
    try {
      const v = renewSchema.parse(values);
      await safetyEnvironmentalComplianceService.renewPermit(renewing.id, {
        newIssueDate: new Date(v.newIssueDate).toISOString(),
        newExpiryDate: new Date(v.newExpiryDate).toISOString(),
        newAuthorityReferenceNumber: blank(v.newAuthorityReferenceNumber),
        notes: blank(v.notes),
      });
      await invalidate();
      toast({ title: 'Permit renewed', description: renewing.registerNumber });
      setRenewing(null);
    } catch (error: any) {
      fail('Renewing the permit failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  // ── Documents ──

  const openDocuments = (row: SheEnvironmentalPermitSummary) => {
    setUploadFile(null);
    setUploadSummary('');
    setDocumentsFor(row);
  };

  const upload = async () => {
    if (!documentsFor || !uploadFile) return;
    setBusy(true);
    try {
      await safetyEnvironmentalComplianceService.uploadPermitDocument(
        documentsFor.id,
        uploadFile,
        uploadSummary.trim() || undefined,
      );
      await invalidate();
      toast({ title: 'Document uploaded', description: documentsFor.registerNumber });
      setUploadFile(null);
      setUploadSummary('');
    } catch (error: any) {
      fail('The upload was refused.')(error);
    } finally {
      setBusy(false);
    }
  };

  const PermitTable = ({
    items,
    emptyText,
  }: {
    items: SheEnvironmentalPermitSummary[];
    emptyText: string;
  }) =>
    items.length === 0 ? (
      <EmptyState title="Nothing here" description={emptyText} icon={FileBadge} />
    ) : (
      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Register #</TableHead>
                <TableHead>Permit</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Authority ref</TableHead>
                <TableHead>Issuing body</TableHead>
                <TableHead>Responsible officer</TableHead>
                <TableHead>Expiry</TableHead>
                <TableHead>Version</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-[60px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((p) => {
                const overdue = p.status === 'Expired' || p.daysToExpiry < 0;
                return (
                  <TableRow key={p.id}>
                    <TableCell className="font-mono">{p.registerNumber}</TableCell>
                    <TableCell className="font-medium">{p.permitName}</TableCell>
                    <TableCell>{p.permitTypeName}</TableCell>
                    <TableCell>{p.authorityReferenceNumber ?? '—'}</TableCell>
                    <TableCell>{p.issuingBodyName ?? '—'}</TableCell>
                    <TableCell>{p.responsibleOfficerName}</TableCell>
                    <TableCell className={overdue ? 'text-destructive font-medium' : undefined}>
                      {fmtDate(p.expiryDate)}
                      <span className="ml-1 text-sm">
                        {p.daysToExpiry < 0
                          ? `(${-p.daysToExpiry} d overdue)`
                          : `(in ${p.daysToExpiry} d)`}
                      </span>
                    </TableCell>
                    <TableCell className="font-mono">{p.currentVersionLabel ?? '—'}</TableCell>
                    <TableCell>
                      <PermitStatusBadge status={p.status} name={p.statusName} />
                    </TableCell>
                    <TableCell>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" className="h-8 w-8">
                            <MoreHorizontal className="h-4 w-4" />
                            <span className="sr-only">Actions</span>
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => openDocuments(p)}>
                            Documents…
                          </DropdownMenuItem>
                          <DropdownMenuItem onClick={() => openEdit(p)}>Edit…</DropdownMenuItem>
                          <DropdownMenuItem onClick={() => markRenewal(p)}>
                            Mark renewal in progress
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            onClick={() => {
                              renewForm.reset({
                                newIssueDate: isoDay(new Date()),
                                newExpiryDate: '',
                                newAuthorityReferenceNumber: '',
                                notes: '',
                              });
                              setRenewing(p);
                            }}
                          >
                            Renew…
                          </DropdownMenuItem>
                          <DropdownMenuItem onClick={() => suspend(p)}>Suspend</DropdownMenuItem>
                          <DropdownMenuItem onClick={() => setPendingArchive(p)}>
                            Archive…
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            className="text-red-600"
                            onClick={() => setPendingDelete(p)}
                          >
                            Delete
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Environmental Permits & Licences"
        description="The FR-ENV-017 register of permits, EPA registrations, licences and consents. The FR-ENV-018 renewal ladder (90/60/30/14/7 days) rides the reminder engine off the expiry dates recorded here; permit documents are versioned on the central DMS through the controlled upload gate."
        backHref="/hr/safety"
        actions={
          <Button onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" /> Register permit
          </Button>
        }
      />

      <Tabs defaultValue="register">
        <TabsList>
          <TabsTrigger value="register">Register ({all.length})</TabsTrigger>
          <TabsTrigger value="expiring">Expiring (90 days) ({expiring.length})</TabsTrigger>
          <TabsTrigger value="expired">Expired ({expired.length})</TabsTrigger>
        </TabsList>
        <TabsContent value="register" className="mt-4">
          <PermitTable items={all} emptyText="No permits on the register yet." />
        </TabsContent>
        <TabsContent value="expiring" className="mt-4">
          <PermitTable items={expiring} emptyText="Nothing expires in the next 90 days." />
        </TabsContent>
        <TabsContent value="expired" className="mt-4">
          <PermitTable items={expired} emptyText="No expired permits." />
        </TabsContent>
      </Tabs>

      {/* ── Create / edit dialog ── */}
      <Dialog open={dialogOpen} onOpenChange={(o) => !busy && setDialogOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[680px]">
          <DialogHeader>
            <DialogTitle>{editing ? `Edit ${editing.number}` : 'Register permit'}</DialogTitle>
            <DialogDescription>
              {editing
                ? 'The register number and the dates are fixed here — renewal owns the dates.'
                : 'Leave the number to the server — it assigns the register number.'}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitPermit} className="space-y-4">
            <FieldRow>
              <TextField form={permitForm} name="permitName" label="Permit name" required />
              <SelectField
                form={permitForm}
                name="permitType"
                label="Type"
                required
                options={SHE_ENV_PERMIT_TYPE_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <TextField
                form={permitForm}
                name="authorityReferenceNumber"
                label="Authority reference"
              />
              <SelectField
                form={permitForm}
                name="issuingBodyId"
                label="Issuing body"
                allowEmpty
                emptyLabel="Not set"
                options={bodyOptions}
              />
            </FieldRow>
            <EmployeePickerField
              form={permitForm}
              name="responsibleOfficerId"
              label="Responsible officer"
              required
            />
            <FieldRow>
              <SelectField
                form={permitForm}
                name="locationId"
                label="Location"
                allowEmpty
                emptyLabel="Not set"
                options={locationOptions}
              />
              <NumberField
                form={permitForm}
                name="renewalPeriodMonths"
                label="Renewal period (months)"
              />
            </FieldRow>
            {!editing && (
              <FieldRow>
                <DateField form={permitForm} name="issueDate" label="Issue date" required />
                <DateField form={permitForm} name="expiryDate" label="Expiry date" required />
              </FieldRow>
            )}
            <TextareaField form={permitForm} name="description" label="Description" rows={2} />
            <TextareaField form={permitForm} name="conditions" label="Conditions" rows={3} />
            <TextareaField form={permitForm} name="notes" label="Notes" rows={2} />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setDialogOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editing ? 'Save' : 'Register permit'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Renew dialog ── */}
      <Dialog open={renewing !== null} onOpenChange={(o) => !busy && !o && setRenewing(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Renew {renewing?.registerNumber}</DialogTitle>
            <DialogDescription>
              Records the renewal, sets the new validity window and returns the permit to Active.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitRenew} className="space-y-4">
            <FieldRow>
              <DateField form={renewForm} name="newIssueDate" label="New issue date" required />
              <DateField form={renewForm} name="newExpiryDate" label="New expiry date" required />
            </FieldRow>
            <TextField
              form={renewForm}
              name="newAuthorityReferenceNumber"
              label="New authority reference"
            />
            <TextareaField form={renewForm} name="notes" label="Notes" rows={2} />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setRenewing(null)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Renew permit
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Documents dialog ── */}
      <Dialog
        open={documentsFor !== null}
        onOpenChange={(o) => !busy && !o && setDocumentsFor(null)}
      >
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[760px]">
          <DialogHeader>
            <DialogTitle>Documents — {documentsFor?.registerNumber}</DialogTitle>
            <DialogDescription>
              Versions live on the central DMS through the controlled upload gate; the version
              number is assigned automatically
              {documentPermit?.currentVersionLabel
                ? ` (current: ${documentPermit.currentVersionLabel})`
                : ' (first upload becomes v1.0)'}
              .
            </DialogDescription>
          </DialogHeader>
          {documentPermit == null ? (
            <div className="text-muted-foreground flex items-center gap-2 py-6 text-sm">
              <Loader2 className="h-4 w-4 animate-spin" /> Loading…
            </div>
          ) : documentPermit.versions.length === 0 ? (
            <EmptyState
              title="No document yet"
              description="Upload the permit document — the register row is only metadata until then."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Version</TableHead>
                  <TableHead>File</TableHead>
                  <TableHead>Size</TableHead>
                  <TableHead>Uploaded</TableHead>
                  <TableHead>By</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {documentPermit.versions.map((v) => (
                  <TableRow key={v.id}>
                    <TableCell className="font-mono font-medium">
                      {v.versionNumber}
                      {v.versionNumber === documentPermit.currentVersionLabel && (
                        <Badge variant="outline" className="ml-2">
                          current
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="max-w-xs">
                      <span className="line-clamp-1">{v.fileName ?? '—'}</span>
                    </TableCell>
                    <TableCell className="tabular-nums">{fmtSize(v.fileSize)}</TableCell>
                    <TableCell className="tabular-nums">
                      {v.uploadedAt ? new Date(v.uploadedAt).toLocaleString() : '—'}
                    </TableCell>
                    <TableCell>{v.uploadedByName}</TableCell>
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() =>
                          void safetyEnvironmentalComplianceService
                            .downloadPermitDocument(
                              documentPermit.id,
                              v.id,
                              v.fileName ?? documentPermit.registerNumber,
                            )
                            .catch((error: any) =>
                              toast({
                                title: 'Download failed',
                                description: error?.message ?? 'The file could not be retrieved.',
                                variant: 'destructive',
                              }),
                            )
                        }
                      >
                        <Download className="mr-1 h-4 w-4" />
                        Get
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
          <div className="space-y-4 border-t pt-4">
            <div className="space-y-2">
              <Label>Upload new version</Label>
              <Input type="file" onChange={(e) => setUploadFile(e.target.files?.[0] ?? null)} />
            </div>
            <div className="space-y-2">
              <Label>Change summary</Label>
              <Textarea
                rows={2}
                value={uploadSummary}
                onChange={(e) => setUploadSummary(e.target.value)}
                placeholder="What changed in this revision (optional)"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" disabled={busy} onClick={() => setDocumentsFor(null)}>
              Close
            </Button>
            <Button disabled={busy || !uploadFile} onClick={() => void upload()}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Upload
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingArchive !== null}
        onOpenChange={(o) => !o && setPendingArchive(null)}
        title={`Archive ${pendingArchive?.registerNumber}?`}
        description="An archived permit keeps its history but leaves the working register."
        confirmText="Archive"
        onConfirm={async () => {
          if (!pendingArchive) return;
          try {
            await safetyEnvironmentalComplianceService.archivePermit(pendingArchive.id);
            await invalidate();
            toast({ title: 'Permit archived', description: pendingArchive.registerNumber });
          } catch (error: any) {
            fail('Archiving failed.')(error);
          } finally {
            setPendingArchive(null);
          }
        }}
      />

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(o) => !o && setPendingDelete(null)}
        title={`Delete ${pendingDelete?.registerNumber}?`}
        description="Only a permit without an uploaded document can be deleted — one with a document archives instead."
        confirmText="Delete"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingDelete) return;
          try {
            await safetyEnvironmentalComplianceService.removePermit(pendingDelete.id);
            await invalidate();
            toast({ title: 'Permit deleted', description: pendingDelete.registerNumber });
          } catch (error: any) {
            fail('Deleting failed.')(error);
          } finally {
            setPendingDelete(null);
          }
        }}
      />
    </div>
  );
}
