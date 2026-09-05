'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { FileCheck, Loader2, MoreHorizontal, Pencil, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  TextareaField,
  SelectField,
  DateField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyRegulatoryService } from '@/services/hr/safety-regulatory.service';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import { SHE_REGULATORY_DOMAIN_OPTIONS } from '@/types/hr/safety';
import type { SheRegulatoryDomain } from '@/types/hr/safety';
import { SHE_COMPLIANCE_STATUS_OPTIONS } from '@/types/hr/safety-governance';
import type {
  SheComplianceStatus,
  SheRegulatoryComplianceEvidence,
} from '@/types/hr/safety-governance';

/**
 * Obligation detail: the statutory duty, its compliance standing and the evidence trail —
 * for a GNFS certification each inspection/certificate is one evidence row with its expiry.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

const editSchema = z.object({
  title: z.string().min(1, 'A title is required').max(300),
  description: z.string().max(2000).optional().or(z.literal('')),
  domain: z.string().min(1),
  legislationName: z.string().max(200).optional().or(z.literal('')),
  sectionOrClause: z.string().max(100).optional().or(z.literal('')),
  regulatoryBodyId: z.string().optional().or(z.literal('')),
  complianceStatus: z.string().min(1),
  complianceNotes: z.string().max(1000).optional().or(z.literal('')),
  obligationOwnerId: z.string().optional().or(z.literal('')),
  lastReviewedDate: z.string().optional().or(z.literal('')),
  nextReviewDate: z.string().optional().or(z.literal('')),
  lastAmendmentNotes: z.string().max(500).optional().or(z.literal('')),
  lastAmendmentDate: z.string().optional().or(z.literal('')),
  isActive: z.boolean(),
});
type EditForm = z.input<typeof editSchema>;

const evidenceSchema = z.object({
  evidenceTitle: z.string().min(1, 'A title is required').max(200),
  description: z.string().max(500).optional().or(z.literal('')),
  evidenceDate: z.string().min(1, 'A date is required'),
  expiryDate: z.string().optional().or(z.literal('')),
  documentPath: z.string().max(500).optional().or(z.literal('')),
  recordedById: z.string().min(1, 'A recorder is required'),
});
type EvidenceForm = z.input<typeof evidenceSchema>;

const statusVariant = (s: SheComplianceStatus) =>
  s === 'NonCompliant' ? 'destructive' : s === 'Compliant' ? 'default' : 'secondary';

const isExpired = (v?: string | null) => !!v && new Date(v).getTime() < Date.now();

export default function RegulatoryObligationDetailPage() {
  const params = useParams<{ id: string }>();
  const obligationId = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [editOpen, setEditOpen] = useState(false);
  const [evidenceOpen, setEvidenceOpen] = useState(false);
  const [pendingRemove, setPendingRemove] = useState<SheRegulatoryComplianceEvidence | null>(null);
  const [busy, setBusy] = useState(false);

  const { data: obligation, isLoading } = useQuery({
    queryKey: ['hr', 'safety-regulatory', 'obligation', obligationId],
    queryFn: () => safetyRegulatoryService.getObligation(obligationId),
    enabled: !!obligationId,
  });
  const { data: bodies = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'regulatory-bodies'],
    queryFn: () => safetyReferenceService.getRegulatoryBodies(true),
  });

  const editForm = useForm<EditForm>({ resolver: zodResolver(editSchema) });
  const evidenceForm = useForm<EvidenceForm>({ resolver: zodResolver(evidenceSchema) });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-regulatory'] });

  const openEdit = () => {
    if (!obligation) return;
    editForm.reset({
      title: obligation.title,
      description: obligation.description ?? '',
      domain: obligation.domain,
      legislationName: obligation.legislationName ?? '',
      sectionOrClause: obligation.sectionOrClause ?? '',
      regulatoryBodyId: obligation.regulatoryBodyId ?? '',
      complianceStatus: obligation.complianceStatus,
      complianceNotes: obligation.complianceNotes ?? '',
      obligationOwnerId: obligation.obligationOwnerId ?? '',
      lastReviewedDate: obligation.lastReviewedDate ? obligation.lastReviewedDate.slice(0, 10) : '',
      nextReviewDate: obligation.nextReviewDate ? obligation.nextReviewDate.slice(0, 10) : '',
      lastAmendmentNotes: obligation.lastAmendmentNotes ?? '',
      lastAmendmentDate: obligation.lastAmendmentDate ? obligation.lastAmendmentDate.slice(0, 10) : '',
      isActive: obligation.isActive,
    });
    setEditOpen(true);
  };

  const submitEdit = editForm.handleSubmit(async (values) => {
    if (!obligation) return;
    setBusy(true);
    try {
      const v = editSchema.parse(values);
      await safetyRegulatoryService.updateObligation(obligation.id, {
        id: obligation.id,
        title: v.title,
        description: blank(v.description),
        domain: v.domain as SheRegulatoryDomain,
        legislationName: blank(v.legislationName),
        sectionOrClause: blank(v.sectionOrClause),
        regulatoryBodyId: blank(v.regulatoryBodyId),
        complianceStatus: v.complianceStatus as SheComplianceStatus,
        complianceNotes: blank(v.complianceNotes),
        obligationOwnerId: blank(v.obligationOwnerId),
        lastReviewedDate: v.lastReviewedDate ? new Date(v.lastReviewedDate).toISOString() : null,
        nextReviewDate: v.nextReviewDate ? new Date(v.nextReviewDate).toISOString() : null,
        lastAmendmentNotes: blank(v.lastAmendmentNotes),
        lastAmendmentDate: v.lastAmendmentDate ? new Date(v.lastAmendmentDate).toISOString() : null,
        isActive: v.isActive,
      });
      await refresh();
      toast({ title: 'Obligation updated' });
      setEditOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Saving the obligation failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  const openAddEvidence = () => {
    evidenceForm.reset({
      evidenceTitle: '',
      description: '',
      evidenceDate: new Date().toISOString().slice(0, 10),
      expiryDate: '',
      documentPath: '',
      recordedById: '',
    });
    setEvidenceOpen(true);
  };

  const submitEvidence = evidenceForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = evidenceSchema.parse(values);
      await safetyRegulatoryService.addEvidence(obligationId, {
        obligationId,
        evidenceTitle: v.evidenceTitle,
        description: blank(v.description),
        evidenceDate: new Date(v.evidenceDate).toISOString(),
        expiryDate: v.expiryDate ? new Date(v.expiryDate).toISOString() : null,
        documentPath: blank(v.documentPath),
        recordedById: v.recordedById,
      });
      await refresh();
      toast({ title: 'Evidence recorded' });
      setEvidenceOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Recording the evidence failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  if (isLoading || !obligation) {
    return (
      <div className="text-muted-foreground flex items-center gap-2 p-6 text-sm">
        <Loader2 className="h-4 w-4 animate-spin" /> Loading obligation…
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${obligation.obligationCode} — ${obligation.title}`}
        description={`${obligation.domainName}${obligation.legislationName ? ` · ${obligation.legislationName}` : ''}${obligation.sectionOrClause ? ` (${obligation.sectionOrClause})` : ''}`}
        backHref="/hr/safety/regulatory"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={openEdit}>
              <Pencil className="mr-2 h-4 w-4" /> Edit
            </Button>
            <Button onClick={openAddEvidence}>
              <Plus className="mr-2 h-4 w-4" /> Record evidence
            </Button>
          </div>
        }
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Standing</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4 text-sm">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <div className="text-muted-foreground">Compliance</div>
              <Badge variant={statusVariant(obligation.complianceStatus)}>
                {obligation.complianceStatusName}
              </Badge>
            </div>
            <div>
              <div className="text-muted-foreground">Regulatory body</div>
              <div className="font-medium">{obligation.regulatoryBodyName ?? '—'}</div>
            </div>
            <div>
              <div className="text-muted-foreground">Owner</div>
              <div className="font-medium">{obligation.obligationOwnerName ?? 'Unassigned'}</div>
            </div>
            <div>
              <div className="text-muted-foreground">Status</div>
              <StatusBadge status={obligation.isActive ? 'Active' : 'Inactive'} />
            </div>
            <div>
              <div className="text-muted-foreground">Last reviewed</div>
              <div className="font-medium">{fmtDate(obligation.lastReviewedDate)}</div>
            </div>
            <div>
              <div className="text-muted-foreground">Next review</div>
              <div
                className={
                  isExpired(obligation.nextReviewDate) ? 'text-destructive font-medium' : 'font-medium'
                }
              >
                {fmtDate(obligation.nextReviewDate)}
              </div>
            </div>
            <div>
              <div className="text-muted-foreground">Last amendment</div>
              <div className="font-medium">{fmtDate(obligation.lastAmendmentDate)}</div>
            </div>
          </div>
          {obligation.description && (
            <div>
              <div className="text-muted-foreground">Description</div>
              <p className="whitespace-pre-wrap">{obligation.description}</p>
            </div>
          )}
          {obligation.complianceNotes && (
            <div>
              <div className="text-muted-foreground">Compliance notes</div>
              <p className="whitespace-pre-wrap">{obligation.complianceNotes}</p>
            </div>
          )}
          {obligation.lastAmendmentNotes && (
            <div>
              <div className="text-muted-foreground">Amendment notes</div>
              <p className="whitespace-pre-wrap">{obligation.lastAmendmentNotes}</p>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="space-y-3">
        <h2 className="text-sm font-medium">
          Evidence ({obligation.evidenceRecords.length})
        </h2>
        {obligation.evidenceRecords.length === 0 ? (
          <EmptyState
            title="No evidence recorded"
            description="Each inspection report, certificate or submission is one evidence row — with its expiry where it has one."
            icon={FileCheck}
          />
        ) : (
          <Card>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Evidence</TableHead>
                    <TableHead>Date</TableHead>
                    <TableHead>Expiry</TableHead>
                    <TableHead>Recorded by</TableHead>
                    <TableHead>Document</TableHead>
                    <TableHead className="w-[60px]" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {obligation.evidenceRecords.map((e) => (
                    <TableRow key={e.id}>
                      <TableCell>
                        <div className="font-medium">{e.evidenceTitle}</div>
                        {e.description && (
                          <div className="text-muted-foreground text-xs">{e.description}</div>
                        )}
                      </TableCell>
                      <TableCell>{fmtDate(e.evidenceDate)}</TableCell>
                      <TableCell>
                        {e.expiryDate ? (
                          <span className={isExpired(e.expiryDate) ? 'text-destructive font-medium' : ''}>
                            {fmtDate(e.expiryDate)}
                            {isExpired(e.expiryDate) && ' (expired)'}
                          </span>
                        ) : (
                          '—'
                        )}
                      </TableCell>
                      <TableCell>{e.recordedByName}</TableCell>
                      <TableCell className="max-w-[200px] truncate font-mono text-xs" title={e.documentPath ?? undefined}>
                        {e.documentPath ?? '—'}
                      </TableCell>
                      <TableCell>
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 text-red-600"
                          onClick={() => setPendingRemove(e)}
                        >
                          <MoreHorizontal className="h-4 w-4" />
                          <span className="sr-only">Remove</span>
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        )}
      </div>

      {/* ── Edit dialog ── */}
      <Dialog open={editOpen} onOpenChange={(o) => !busy && setEditOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>Edit obligation</DialogTitle>
            <DialogDescription>
              The code (<span className="font-mono">{obligation.obligationCode}</span>) is fixed at
              creation.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitEdit} className="space-y-4">
            <TextField form={editForm} name="title" label="Title" required />
            <TextareaField form={editForm} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={editForm}
                name="domain"
                label="Domain"
                required
                options={SHE_REGULATORY_DOMAIN_OPTIONS}
              />
              <SelectField
                form={editForm}
                name="regulatoryBodyId"
                label="Regulatory body"
                allowEmpty
                emptyLabel="Not set"
                options={bodies.map((b) => ({ value: b.id, label: b.shortName ?? b.name }))}
              />
            </FieldRow>
            <FieldRow>
              <TextField form={editForm} name="legislationName" label="Legislation" />
              <TextField form={editForm} name="sectionOrClause" label="Section / clause" />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={editForm}
                name="complianceStatus"
                label="Compliance status"
                required
                options={SHE_COMPLIANCE_STATUS_OPTIONS}
              />
              <div className="self-end pb-1">
                <SwitchField form={editForm} name="isActive" label="Active" />
              </div>
            </FieldRow>
            <TextareaField form={editForm} name="complianceNotes" label="Compliance notes" rows={2} />
            <FieldRow>
              <DateField form={editForm} name="lastReviewedDate" label="Last reviewed" />
              <DateField form={editForm} name="nextReviewDate" label="Next review" />
            </FieldRow>
            <FieldRow>
              <DateField form={editForm} name="lastAmendmentDate" label="Last amendment" />
              <TextField form={editForm} name="lastAmendmentNotes" label="Amendment notes" />
            </FieldRow>
            <EmployeePickerField form={editForm} name="obligationOwnerId" label="Obligation owner" />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setEditOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Save
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Evidence dialog ── */}
      <Dialog open={evidenceOpen} onOpenChange={(o) => !busy && setEvidenceOpen(o)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>Record evidence</DialogTitle>
            <DialogDescription>
              One row per inspection report, certificate or submission. Set the expiry for
              certificates that lapse (e.g. a GNFS fire certificate).
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitEvidence} className="space-y-4">
            <TextField form={evidenceForm} name="evidenceTitle" label="Title" required />
            <TextareaField form={evidenceForm} name="description" label="Description" rows={2} />
            <FieldRow>
              <DateField form={evidenceForm} name="evidenceDate" label="Evidence date" required />
              <DateField form={evidenceForm} name="expiryDate" label="Expiry" />
            </FieldRow>
            <TextField form={evidenceForm} name="documentPath" label="Document path" />
            <EmployeePickerField form={evidenceForm} name="recordedById" label="Recorded by" required />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setEvidenceOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Record
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingRemove !== null}
        onOpenChange={(open) => !open && setPendingRemove(null)}
        title={`Remove "${pendingRemove?.evidenceTitle}"?`}
        description="This removes the evidence row from the obligation's trail."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingRemove) return;
          try {
            await safetyRegulatoryService.removeEvidence(pendingRemove.id);
            await refresh();
            toast({ title: 'Evidence removed' });
          } catch (error: any) {
            toast({
              title: 'Error',
              description: error?.message || 'Removing failed.',
              variant: 'destructive',
            });
          } finally {
            setPendingRemove(null);
          }
        }}
      />
    </div>
  );
}
