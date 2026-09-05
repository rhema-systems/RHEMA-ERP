'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Check, Loader2, Minus, MoreHorizontal, Plus, Scale } from 'lucide-react';
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
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  TextField,
  TextareaField,
  SelectField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyEnvironmentalComplianceService } from '@/services/hr/safety-environmental-compliance.service';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import { SHE_REGULATORY_DOMAIN_OPTIONS } from '@/types/hr/safety';
import { SHE_COMPLIANCE_STATUS_OPTIONS } from '@/types/hr/safety-governance';
import {
  SHE_REG_UPDATE_RISK_OPTIONS,
  SHE_REG_UPDATE_STATUS_OPTIONS,
} from '@/types/hr/safety-environment-compliance';
import type {
  SheRegulatoryUpdateSummary,
  SheRegulatoryUpdateRiskLevel,
  SheRegulatoryUpdateStatus,
} from '@/types/hr/safety-environment-compliance';

/**
 * Regulatory updates register (FR-ENV-030–032, built once with FR-SHE-182):
 * new LIs and standards from EPA, GSA, ministries and international bodies —
 * assessed (summary, affected departments, deadline, risk, actions),
 * communicated to management (one-shot escalated notice) and tracked to
 * compliance closure. Deadline reminders ride the reminder engine.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const isoDay = (d: Date) => d.toISOString().slice(0, 10);
const isoOrNull = (v?: string) => (v && v.length > 0 ? new Date(v).toISOString() : null);

const updateSchema = z.object({
  title: z.string().min(1, 'A title is required').max(300),
  regulationReference: z.string().max(100).optional().or(z.literal('')),
  regulatoryBodyId: z.string().optional().or(z.literal('')),
  authorityName: z.string().max(200).optional().or(z.literal('')),
  domain: z.string().min(1),
  summary: z.string().min(1, 'A summary is required').max(2000),
  issueDate: z.string().min(1, 'The issue date is required'),
  effectiveDate: z.string().optional().or(z.literal('')),
  affectedDepartments: z.string().max(500).optional().or(z.literal('')),
  complianceDeadline: z.string().optional().or(z.literal('')),
  riskLevel: z.string().min(1),
  requiredActions: z.string().max(2000).optional().or(z.literal('')),
  reviewDate: z.string().optional().or(z.literal('')),
  officerComments: z.string().max(1000).optional().or(z.literal('')),
  // Edit-only.
  status: z.string().optional().or(z.literal('')),
  complianceStatus: z.string().optional().or(z.literal('')),
});
type UpdateForm = z.input<typeof updateSchema>;

const closeSchema = z.object({
  complianceStatus: z.string().min(1),
  closureNotes: z.string().max(1000).optional().or(z.literal('')),
});
type CloseForm = z.input<typeof closeSchema>;

function RiskBadge({ level, name }: { level: SheRegulatoryUpdateRiskLevel; name: string }) {
  switch (level) {
    case 'Critical':
      return <Badge variant="destructive">{name}</Badge>;
    case 'High':
      return (
        <Badge variant="outline" className="border-amber-500 text-amber-600">
          {name}
        </Badge>
      );
    case 'Medium':
      return <Badge variant="secondary">{name}</Badge>;
    default:
      return <Badge variant="outline">{name}</Badge>;
  }
}

function UpdateStatusBadge({ status, name }: { status: SheRegulatoryUpdateStatus; name: string }) {
  switch (status) {
    case 'Closed':
      return <Badge>{name}</Badge>;
    case 'ActionsInProgress':
      return <Badge variant="secondary">Actions in progress</Badge>;
    default:
      return <Badge variant="outline">{name}</Badge>;
  }
}

// The server refuses closing through the update endpoint — closure has its own action.
const EDIT_STATUS_OPTIONS = SHE_REG_UPDATE_STATUS_OPTIONS.filter((o) => o.value !== 'Closed');

export default function RegulatoryUpdatesPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<{ id: string; number: string } | null>(null);
  const [closing, setClosing] = useState<SheRegulatoryUpdateSummary | null>(null);
  const [pendingNotify, setPendingNotify] = useState<SheRegulatoryUpdateSummary | null>(null);
  const [pendingDelete, setPendingDelete] = useState<SheRegulatoryUpdateSummary | null>(null);
  const [busy, setBusy] = useState(false);

  const { data: all = [] } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'regulatory-updates', 'all'],
    queryFn: () => safetyEnvironmentalComplianceService.getRegulatoryUpdates(),
  });
  const { data: bodies = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'regulatory-bodies'],
    queryFn: () => safetyReferenceService.getRegulatoryBodies(true),
  });

  const open = all.filter((u) => u.status !== 'Closed');
  const closed = all.filter((u) => u.status === 'Closed');

  const updateForm = useForm<UpdateForm>({ resolver: zodResolver(updateSchema) });
  const closeForm = useForm<CloseForm>({ resolver: zodResolver(closeSchema) });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-env-compliance'] });
  const fail = (fallback: string) => (error: any) =>
    toast({ title: 'Error', description: error?.message || fallback, variant: 'destructive' });

  const bodyOptions = bodies.map((b) => ({
    value: b.id,
    label: b.shortName ? `${b.shortName} — ${b.name}` : b.name,
  }));

  const openCreate = () => {
    setEditing(null);
    updateForm.reset({
      title: '',
      regulationReference: '',
      regulatoryBodyId: '',
      authorityName: '',
      domain: 'EnvironmentalProtection',
      summary: '',
      issueDate: isoDay(new Date()),
      effectiveDate: '',
      affectedDepartments: '',
      complianceDeadline: '',
      riskLevel: 'Medium',
      requiredActions: '',
      reviewDate: '',
      officerComments: '',
      status: '',
      complianceStatus: '',
    });
    setDialogOpen(true);
  };

  const openEdit = async (row: SheRegulatoryUpdateSummary) => {
    try {
      const full = await safetyEnvironmentalComplianceService.getRegulatoryUpdate(row.id);
      updateForm.reset({
        title: full.title,
        regulationReference: full.regulationReference ?? '',
        regulatoryBodyId: full.regulatoryBodyId ?? '',
        authorityName: full.authorityName ?? '',
        domain: full.domain,
        summary: full.summary,
        issueDate: full.issueDate.slice(0, 10),
        effectiveDate: full.effectiveDate?.slice(0, 10) ?? '',
        affectedDepartments: full.affectedDepartments ?? '',
        complianceDeadline: full.complianceDeadline?.slice(0, 10) ?? '',
        riskLevel: full.riskLevel,
        requiredActions: full.requiredActions ?? '',
        reviewDate: full.reviewDate?.slice(0, 10) ?? '',
        officerComments: full.officerComments ?? '',
        status: full.status,
        complianceStatus: full.complianceStatus,
      });
      setEditing({ id: full.id, number: full.updateNumber });
      setDialogOpen(true);
    } catch (error: any) {
      fail('Loading the update failed.')(error);
    }
  };

  const submitUpdate = updateForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = updateSchema.parse(values);
      const common = {
        title: v.title,
        regulationReference: blank(v.regulationReference),
        regulatoryBodyId: blank(v.regulatoryBodyId),
        authorityName: blank(v.authorityName),
        domain: v.domain,
        summary: v.summary,
        issueDate: new Date(v.issueDate).toISOString(),
        effectiveDate: isoOrNull(v.effectiveDate),
        affectedDepartments: blank(v.affectedDepartments),
        complianceDeadline: isoOrNull(v.complianceDeadline),
        riskLevel: v.riskLevel as SheRegulatoryUpdateRiskLevel,
        requiredActions: blank(v.requiredActions),
        reviewDate: isoOrNull(v.reviewDate),
        officerComments: blank(v.officerComments),
      };
      if (editing) {
        const saved = await safetyEnvironmentalComplianceService.updateRegulatoryUpdate(editing.id, {
          id: editing.id,
          ...common,
          status: (v.status || 'Recorded') as SheRegulatoryUpdateStatus,
          complianceStatus: v.complianceStatus || 'NotAssessed',
        });
        await invalidate();
        toast({ title: 'Update saved', description: saved.updateNumber });
      } else {
        const saved = await safetyEnvironmentalComplianceService.createRegulatoryUpdate(common);
        await invalidate();
        toast({ title: 'Regulatory update recorded', description: saved.updateNumber });
      }
      setDialogOpen(false);
    } catch (error: any) {
      fail('Saving the regulatory update failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  const submitClose = closeForm.handleSubmit(async (values) => {
    if (!closing) return;
    setBusy(true);
    try {
      const v = closeSchema.parse(values);
      await safetyEnvironmentalComplianceService.closeRegulatoryUpdate(closing.id, {
        complianceStatus: v.complianceStatus,
        closureNotes: blank(v.closureNotes),
      });
      await invalidate();
      toast({ title: 'Update closed', description: closing.updateNumber });
      setClosing(null);
    } catch (error: any) {
      fail('Closing the update failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  const UpdateTable = ({
    items,
    emptyText,
  }: {
    items: SheRegulatoryUpdateSummary[];
    emptyText: string;
  }) =>
    items.length === 0 ? (
      <EmptyState title="Nothing here" description={emptyText} icon={Scale} />
    ) : (
      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Update #</TableHead>
                <TableHead>Title</TableHead>
                <TableHead>Reference</TableHead>
                <TableHead>Authority</TableHead>
                <TableHead>Issued</TableHead>
                <TableHead>Deadline</TableHead>
                <TableHead>Risk</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Compliance</TableHead>
                <TableHead>Mgmt notified</TableHead>
                <TableHead className="w-[60px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((u) => {
                const overdue =
                  u.status !== 'Closed' &&
                  !!u.complianceDeadline &&
                  new Date(u.complianceDeadline) < new Date();
                return (
                  <TableRow key={u.id}>
                    <TableCell className="font-mono">{u.updateNumber}</TableCell>
                    <TableCell className="max-w-[280px] truncate font-medium" title={u.title}>
                      {u.title}
                    </TableCell>
                    <TableCell>{u.regulationReference ?? '—'}</TableCell>
                    <TableCell>{u.authorityName ?? '—'}</TableCell>
                    <TableCell>{fmtDate(u.issueDate)}</TableCell>
                    <TableCell className={overdue ? 'text-destructive font-medium' : undefined}>
                      {fmtDate(u.complianceDeadline)}
                    </TableCell>
                    <TableCell>
                      <RiskBadge level={u.riskLevel} name={u.riskLevelName} />
                    </TableCell>
                    <TableCell>
                      <UpdateStatusBadge status={u.status} name={u.statusName} />
                    </TableCell>
                    <TableCell>{u.complianceStatusName}</TableCell>
                    <TableCell>
                      {u.managementNotified ? (
                        <Check className="h-4 w-4 text-green-600" />
                      ) : (
                        <Minus className="text-muted-foreground h-4 w-4" />
                      )}
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
                          <DropdownMenuItem onClick={() => openEdit(u)}>
                            View / edit…
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            disabled={u.managementNotified || u.status === 'Closed'}
                            onClick={() => setPendingNotify(u)}
                          >
                            Notify management…
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            disabled={u.status === 'Closed'}
                            onClick={() => {
                              closeForm.reset({ complianceStatus: 'Compliant', closureNotes: '' });
                              setClosing(u);
                            }}
                          >
                            Close…
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            className="text-red-600"
                            onClick={() => setPendingDelete(u)}
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
        title="Regulatory Updates"
        description="New Legislative Instruments and standards from the EPA, the Ghana Standards Authority, ministries and international bodies (FR-ENV-030–032) — assessed, communicated to management and tracked to compliance closure."
        backHref="/hr/safety"
        actions={
          <Button onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" /> Record update
          </Button>
        }
      />

      <Tabs defaultValue="open">
        <TabsList>
          <TabsTrigger value="open">Open ({open.length})</TabsTrigger>
          <TabsTrigger value="closed">Closed ({closed.length})</TabsTrigger>
          <TabsTrigger value="all">All ({all.length})</TabsTrigger>
        </TabsList>
        <TabsContent value="open" className="mt-4">
          <UpdateTable items={open} emptyText="No open regulatory updates." />
        </TabsContent>
        <TabsContent value="closed" className="mt-4">
          <UpdateTable items={closed} emptyText="Nothing closed yet." />
        </TabsContent>
        <TabsContent value="all" className="mt-4">
          <UpdateTable items={all} emptyText="No regulatory updates recorded yet." />
        </TabsContent>
      </Tabs>

      {/* ── Create / edit dialog ── */}
      <Dialog open={dialogOpen} onOpenChange={(o) => !busy && setDialogOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[680px]">
          <DialogHeader>
            <DialogTitle>{editing ? `Edit ${editing.number}` : 'Record regulatory update'}</DialogTitle>
            <DialogDescription>
              The FR-ENV-031 assessment: summary, affected departments, deadline, risk and required
              actions. Closure has its own action.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitUpdate} className="space-y-4">
            <TextField form={updateForm} name="title" label="Title" required />
            <FieldRow>
              <TextField form={updateForm} name="regulationReference" label="Regulation / LI number" />
              <SelectField
                form={updateForm}
                name="domain"
                label="Domain"
                required
                options={SHE_REGULATORY_DOMAIN_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={updateForm}
                name="regulatoryBodyId"
                label="Regulatory body"
                allowEmpty
                emptyLabel="Not on the register"
                options={bodyOptions}
              />
              <TextField
                form={updateForm}
                name="authorityName"
                label="Authority (free text)"
                placeholder="When the authority is not on the register"
              />
            </FieldRow>
            <TextareaField form={updateForm} name="summary" label="Summary" rows={3} required />
            <FieldRow>
              <DateField form={updateForm} name="issueDate" label="Issue date" required />
              <DateField form={updateForm} name="effectiveDate" label="Effective date" />
            </FieldRow>
            <TextField form={updateForm} name="affectedDepartments" label="Affected departments" />
            <FieldRow>
              <DateField form={updateForm} name="complianceDeadline" label="Compliance deadline" />
              <SelectField
                form={updateForm}
                name="riskLevel"
                label="Risk level"
                required
                options={SHE_REG_UPDATE_RISK_OPTIONS}
              />
            </FieldRow>
            <TextareaField form={updateForm} name="requiredActions" label="Required actions" rows={2} />
            {editing && (
              <FieldRow>
                <SelectField
                  form={updateForm}
                  name="status"
                  label="Status"
                  options={EDIT_STATUS_OPTIONS}
                />
                <SelectField
                  form={updateForm}
                  name="complianceStatus"
                  label="Compliance status"
                  options={SHE_COMPLIANCE_STATUS_OPTIONS}
                />
              </FieldRow>
            )}
            <FieldRow>
              <DateField form={updateForm} name="reviewDate" label="Review date" />
              <TextField form={updateForm} name="officerComments" label="Officer comments" />
            </FieldRow>
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editing ? 'Save' : 'Record update'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Close dialog ── */}
      <Dialog open={closing !== null} onOpenChange={(o) => !busy && !o && setClosing(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Close {closing?.updateNumber}</DialogTitle>
            <DialogDescription>
              Settles the compliance outcome; a closed update is history and refuses further edits.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitClose} className="space-y-4">
            <SelectField
              form={closeForm}
              name="complianceStatus"
              label="Compliance outcome"
              required
              options={SHE_COMPLIANCE_STATUS_OPTIONS}
            />
            <TextareaField form={closeForm} name="closureNotes" label="Closure notes" rows={2} />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setClosing(null)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Close update
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingNotify !== null}
        onOpenChange={(o) => !o && setPendingNotify(null)}
        title={`Notify management about ${pendingNotify?.updateNumber}?`}
        description="Sends the one-shot FR-ENV-031 management notice through the escalated notification topic. It cannot be repeated."
        confirmText="Notify management"
        onConfirm={async () => {
          if (!pendingNotify) return;
          try {
            await safetyEnvironmentalComplianceService.notifyManagement(pendingNotify.id);
            await invalidate();
            toast({ title: 'Management notified', description: pendingNotify.updateNumber });
          } catch (error: any) {
            fail('The notification was refused.')(error);
          } finally {
            setPendingNotify(null);
          }
        }}
      />

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(o) => !o && setPendingDelete(null)}
        title={`Delete ${pendingDelete?.updateNumber}?`}
        description="Only an update that has not been closed or communicated to management can be deleted."
        confirmText="Delete"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingDelete) return;
          try {
            await safetyEnvironmentalComplianceService.removeRegulatoryUpdate(pendingDelete.id);
            await invalidate();
            toast({ title: 'Update deleted', description: pendingDelete.updateNumber });
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
