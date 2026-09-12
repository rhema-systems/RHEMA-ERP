'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, MoreHorizontal, Plus, Recycle } from 'lucide-react';
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
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyWasteService } from '@/services/hr/safety-waste.service';
import { safetyContractorService } from '@/services/hr/safety-contractor.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_WASTE_CLASSIFICATION_OPTIONS,
  SHE_WASTE_UNIT_OPTIONS,
  SHE_WASTE_DISPOSAL_METHOD_OPTIONS,
} from '@/types/hr/safety-environment';
import type {
  SheWasteType,
  SheWasteClassification,
  SheWasteMeasurementUnit,
  SheWasteDisposalMethod,
  SheWasteDisposalRecordSummary,
} from '@/types/hr/safety-environment';

/**
 * Waste management (FR-SHE-060–062): the disposal register (last 90 days — the backend reads
 * by date range) and the waste-type catalogue. The disposal-certificate gate is live: a record
 * for a manifest-requiring type cannot be saved without its manifest number and certificate
 * document — the form marks the requirement when such a type is selected.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

const typeSchema = z.object({
  code: z.string().min(1, 'A code is required').max(20),
  name: z.string().min(1, 'A name is required').max(150),
  classification: z.string().min(1),
  disposalRequirements: z.string().max(500).optional().or(z.literal('')),
  regulatoryReference: z.string().max(200).optional().or(z.literal('')),
  requiresManifest: z.boolean(),
  isActive: z.boolean(),
});
type TypeForm = z.input<typeof typeSchema>;

const recordSchema = z.object({
  wasteTypeId: z.string().min(1, 'A waste type is required'),
  locationId: z.string().optional().or(z.literal('')),
  generationArea: z.string().max(200).optional().or(z.literal('')),
  disposalDate: z.string().min(1, 'A disposal date is required'),
  quantity: z.coerce.number().positive('A quantity is required'),
  unit: z.string().min(1),
  disposalMethod: z.string().min(1),
  wasteContractorId: z.string().optional().or(z.literal('')),
  manifestNumber: z.string().max(100).optional().or(z.literal('')),
  disposalSite: z.string().max(500).optional().or(z.literal('')),
  notes: z.string().max(500).optional().or(z.literal('')),
  recordedById: z.string().min(1, 'A recorder is required'),
  documentPath: z.string().max(500).optional().or(z.literal('')),
});
type RecordForm = z.input<typeof recordSchema>;

const isoDay = (d: Date) => d.toISOString().slice(0, 10);

export default function WastePage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [recordDialogOpen, setRecordDialogOpen] = useState(false);
  const [editingRecordId, setEditingRecordId] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<SheWasteDisposalRecordSummary | null>(null);
  const [busy, setBusy] = useState(false);

  const from = isoDay(new Date(Date.now() - 90 * 86400000));
  const to = isoDay(new Date(Date.now() + 86400000));

  const { data: records = [], isLoading } = useQuery({
    queryKey: ['hr', 'safety-waste', 'records', from, to],
    queryFn: () => safetyWasteService.getRecordsByDateRange(from, to),
  });
  const { data: types = [] } = useQuery({
    queryKey: ['hr', 'safety-waste', 'types'],
    queryFn: () => safetyWasteService.getTypes(),
  });
  const { data: contractors = [] } = useQuery({
    queryKey: ['hr', 'safety-contractors'],
    queryFn: () => safetyContractorService.getAll(),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const recordForm = useForm<RecordForm>({ resolver: zodResolver(recordSchema) });
  const watchedTypeId = recordForm.watch('wasteTypeId');
  const selectedType = types.find((t) => t.id === watchedTypeId);

  const openCreateRecord = () => {
    setEditingRecordId(null);
    recordForm.reset({
      wasteTypeId: '',
      locationId: '',
      generationArea: '',
      disposalDate: isoDay(new Date()),
      quantity: undefined as unknown as number,
      unit: 'Kilograms',
      disposalMethod: 'Landfill',
      wasteContractorId: '',
      manifestNumber: '',
      disposalSite: '',
      notes: '',
      recordedById: '',
      documentPath: '',
    });
    setRecordDialogOpen(true);
  };

  const openEditRecord = async (row: SheWasteDisposalRecordSummary) => {
    const full = await safetyWasteService.getRecord(row.id);
    recordForm.reset({
      wasteTypeId: full.wasteTypeId,
      locationId: full.locationId ?? '',
      generationArea: full.generationArea ?? '',
      disposalDate: full.disposalDate.slice(0, 10),
      quantity: full.quantity,
      unit: full.unit,
      disposalMethod: full.disposalMethod,
      wasteContractorId: full.wasteContractorId ?? '',
      manifestNumber: full.manifestNumber ?? '',
      disposalSite: full.disposalSite ?? '',
      notes: full.notes ?? '',
      recordedById: full.recordedById,
      documentPath: full.documentPath ?? '',
    });
    setEditingRecordId(full.id);
    setRecordDialogOpen(true);
  };

  const submitRecord = recordForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = recordSchema.parse(values);
      const common = {
        wasteTypeId: v.wasteTypeId,
        locationId: blank(v.locationId),
        generationArea: blank(v.generationArea),
        disposalDate: new Date(v.disposalDate).toISOString(),
        quantity: v.quantity,
        unit: v.unit as SheWasteMeasurementUnit,
        disposalMethod: v.disposalMethod as SheWasteDisposalMethod,
        wasteContractorId: blank(v.wasteContractorId),
        manifestNumber: blank(v.manifestNumber),
        disposalSite: blank(v.disposalSite),
        notes: blank(v.notes),
        documentPath: blank(v.documentPath),
      };
      if (editingRecordId) {
        await safetyWasteService.updateRecord(editingRecordId, { id: editingRecordId, ...common });
      } else {
        await safetyWasteService.createRecord({ ...common, recordedById: v.recordedById });
      }
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-waste'] });
      toast({ title: editingRecordId ? 'Disposal record updated' : 'Disposal recorded' });
      setRecordDialogOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Saving the record failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Waste Management"
        description="Disposal records and the waste-type catalogue. Manifest-requiring types cannot be recorded without their manifest number and disposal certificate — the gate is enforced, not advisory."
        backHref="/hr/safety"
        actions={
          <Button onClick={openCreateRecord}>
            <Plus className="mr-2 h-4 w-4" /> Record disposal
          </Button>
        }
      />

      <Tabs defaultValue="records">
        <TabsList>
          <TabsTrigger value="records">Disposal records ({records.length})</TabsTrigger>
          <TabsTrigger value="types">Waste types ({types.length})</TabsTrigger>
        </TabsList>

        {/* ── Disposal records (last 90 days) ── */}
        <TabsContent value="records" className="mt-4">
          <p className="text-muted-foreground mb-3 text-sm">
            Showing the last 90 days — the register reads by date range.
          </p>
          {isLoading ? null : records.length === 0 ? (
            <EmptyState
              title="No disposals in the last 90 days"
              description="Record a disposal to start the trail."
              icon={Recycle}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Record</TableHead>
                      <TableHead>Waste type</TableHead>
                      <TableHead>Date</TableHead>
                      <TableHead>Quantity</TableHead>
                      <TableHead>Method</TableHead>
                      <TableHead>Contractor</TableHead>
                      <TableHead className="w-[60px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {records.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell className="font-mono">{r.recordNumber}</TableCell>
                        <TableCell className="font-medium">{r.wasteTypeName}</TableCell>
                        <TableCell>{fmtDate(r.disposalDate)}</TableCell>
                        <TableCell>
                          {r.quantity} {r.unit}
                        </TableCell>
                        <TableCell>
                          {SHE_WASTE_DISPOSAL_METHOD_OPTIONS.find((o) => o.value === r.disposalMethod)
                            ?.label ?? r.disposalMethod}
                        </TableCell>
                        <TableCell>{r.wasteContractorName ?? '—'}</TableCell>
                        <TableCell>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon" className="h-8 w-8">
                                <MoreHorizontal className="h-4 w-4" />
                                <span className="sr-only">Actions</span>
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onClick={() => openEditRecord(r)}>
                                Edit…
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                className="text-red-600"
                                onClick={() => setPendingDelete(r)}
                              >
                                Remove
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* ── Waste types ── */}
        <TabsContent value="types" className="mt-4">
          <ResourceListPanel<SheWasteType, TypeForm>
            title="waste types"
            singular="waste type"
            queryKey={['hr', 'safety-waste', 'types']}
            list={() => safetyWasteService.getTypes()}
            create={(values) => {
              const v = typeSchema.parse(values);
              return safetyWasteService.createType({
                code: v.code,
                name: v.name,
                classification: v.classification as SheWasteClassification,
                disposalRequirements: blank(v.disposalRequirements),
                regulatoryReference: blank(v.regulatoryReference),
                requiresManifest: v.requiresManifest,
                isActive: v.isActive,
              });
            }}
            update={(id, values) => {
              const v = typeSchema.parse(values);
              return safetyWasteService.updateType(id, {
                id,
                name: v.name,
                classification: v.classification as SheWasteClassification,
                disposalRequirements: blank(v.disposalRequirements),
                regulatoryReference: blank(v.regulatoryReference),
                requiresManifest: v.requiresManifest,
                isActive: v.isActive,
              });
            }}
            remove={(id) => safetyWasteService.removeType(id)}
            columns={[
              { header: 'Code', cell: (t) => <span className="font-mono">{t.code}</span> },
              { header: 'Waste type', cell: (t) => <span className="font-medium">{t.name}</span> },
              {
                header: 'Classification',
                cell: (t) => (
                  <Badge variant={t.classification === 'Hazardous' ? 'destructive' : 'secondary'}>
                    {SHE_WASTE_CLASSIFICATION_OPTIONS.find((o) => o.value === t.classification)
                      ?.label ?? t.classification}
                  </Badge>
                ),
              },
              {
                header: 'Manifest',
                cell: (t) =>
                  t.requiresManifest ? (
                    <Badge variant="default">Required</Badge>
                  ) : (
                    <span className="text-muted-foreground text-sm">Not required</span>
                  ),
              },
              { header: 'Regulatory ref.', cell: (t) => t.regulatoryReference ?? '—' },
              {
                header: 'Status',
                cell: (t) => <StatusBadge status={t.isActive ? 'Active' : 'Inactive'} />,
              },
            ]}
            schema={typeSchema}
            emptyForm={{
              code: '',
              name: '',
              classification: 'NonHazardous',
              disposalRequirements: '',
              regulatoryReference: '',
              requiresManifest: false,
              isActive: true,
            }}
            toForm={(t) => ({
              code: t.code,
              name: t.name,
              classification: t.classification,
              disposalRequirements: t.disposalRequirements ?? '',
              regulatoryReference: t.regulatoryReference ?? '',
              requiresManifest: t.requiresManifest,
              isActive: t.isActive,
            })}
            renderFields={(f, editing) => (
              <>
                <FieldRow>
                  {!editing ? (
                    <TextField form={f} name="code" label="Code (unique)" required />
                  ) : (
                    <div className="text-muted-foreground self-end pb-2 text-sm">
                      <span className="font-mono">{f.getValues('code')}</span> — fixed at creation.
                    </div>
                  )}
                  <TextField form={f} name="name" label="Name" required />
                </FieldRow>
                <SelectField
                  form={f}
                  name="classification"
                  label="Classification"
                  required
                  options={SHE_WASTE_CLASSIFICATION_OPTIONS}
                />
                <TextareaField
                  form={f}
                  name="disposalRequirements"
                  label="Disposal requirements"
                  rows={2}
                />
                <TextField form={f} name="regulatoryReference" label="Regulatory reference" />
                <SwitchField
                  form={f}
                  name="requiresManifest"
                  label="Requires manifest"
                  description="Disposal records for this type cannot be saved without a manifest number and certificate document."
                />
                <SwitchField form={f} name="isActive" label="Active" />
              </>
            )}
            getId={(t) => t.id}
            emptyDescription="The catalogue disposal records classify against — hazardous types should require a manifest."
          />
        </TabsContent>
      </Tabs>

      {/* ── Disposal record dialog (full record — table rows are summaries) ── */}
      <Dialog open={recordDialogOpen} onOpenChange={(o) => !busy && setRecordDialogOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>{editingRecordId ? 'Edit disposal record' : 'Record disposal'}</DialogTitle>
            <DialogDescription>
              {editingRecordId
                ? 'The record number and recorder are fixed at creation.'
                : 'Leave the number to the server — it assigns WD-YYYY-NNNN.'}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitRecord} className="space-y-4">
            <SelectField
              form={recordForm}
              name="wasteTypeId"
              label="Waste type"
              required
              options={types.map((t) => ({
                value: t.id,
                label: `${t.name}${t.requiresManifest ? ' (manifest required)' : ''}`,
              }))}
            />
            {selectedType?.requiresManifest && (
              <p className="text-destructive text-sm">
                This waste type requires a manifest: the manifest number and certificate document
                below are mandatory — the record will be refused without them.
              </p>
            )}
            <FieldRow>
              <SelectField
                form={recordForm}
                name="locationId"
                label="Location"
                allowEmpty
                emptyLabel="Not set"
                options={locations.map((l) => ({ value: l.id, label: l.name }))}
              />
              <TextField form={recordForm} name="generationArea" label="Generation area" />
            </FieldRow>
            <FieldRow>
              <DateField form={recordForm} name="disposalDate" label="Disposal date" required />
              <NumberField form={recordForm} name="quantity" label="Quantity" required />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={recordForm}
                name="unit"
                label="Unit"
                required
                options={SHE_WASTE_UNIT_OPTIONS}
              />
              <SelectField
                form={recordForm}
                name="disposalMethod"
                label="Disposal method"
                required
                options={SHE_WASTE_DISPOSAL_METHOD_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={recordForm}
                name="wasteContractorId"
                label="Waste contractor"
                allowEmpty
                emptyLabel="None / in-house"
                options={contractors.map((c) => ({
                  value: c.id,
                  label: `${c.contractorCode} — ${c.companyName}`,
                }))}
              />
              <TextField form={recordForm} name="manifestNumber" label="Manifest number" />
            </FieldRow>
            <FieldRow>
              <TextField form={recordForm} name="disposalSite" label="Disposal site" />
              <TextField
                form={recordForm}
                name="documentPath"
                label="Certificate document path"
              />
            </FieldRow>
            <TextareaField form={recordForm} name="notes" label="Notes" rows={2} />
            {!editingRecordId && (
              <EmployeePickerField form={recordForm} name="recordedById" label="Recorded by" required />
            )}
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setRecordDialogOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editingRecordId ? 'Save' : 'Record disposal'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title={`Remove ${pendingDelete?.recordNumber}?`}
        description="This removes the disposal record from the register."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingDelete) return;
          try {
            await safetyWasteService.removeRecord(pendingDelete.id);
            await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-waste'] });
            toast({ title: 'Removed', description: pendingDelete.recordNumber });
          } catch (error: any) {
            toast({
              title: 'Error',
              description: error?.message || 'Removing failed.',
              variant: 'destructive',
            });
          } finally {
            setPendingDelete(null);
          }
        }}
      />
    </div>
  );
}
