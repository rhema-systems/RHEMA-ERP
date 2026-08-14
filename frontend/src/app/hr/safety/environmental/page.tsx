'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Leaf, Loader2, MoreHorizontal, Plus } from 'lucide-react';
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
import { safetyEnvironmentalService } from '@/services/hr/safety-environmental.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_ENV_INCIDENT_TYPE_OPTIONS,
  SHE_ENV_MEDIA_OPTIONS,
  SHE_INCIDENT_SEVERITY_OPTIONS,
  SHE_ENV_INCIDENT_EDIT_STATUS_OPTIONS,
  SHE_ENV_MONITORING_TYPE_OPTIONS,
} from '@/types/hr/safety-environment';
import type {
  SheEnvironmentalIncidentSummary,
  SheEnvironmentalIncidentType,
  SheEnvironmentalMedia,
  SheIncidentSeverity,
  SheEnvironmentalIncidentStatus,
  SheEnvironmentalMonitoringRecord,
  SheEnvironmentalMonitoringType,
} from '@/types/hr/safety-environment';

/**
 * Environmental data capture (FR-SHE-050–072): incidents (spills, exceedances, contamination)
 * with EPA-notification tracking and a guarded close flow, plus monitoring readings whose
 * exceedance flags the server computes from the entered limits. EPA reporting is a hand-kept
 * flag — nothing is submitted anywhere automatically. Registers read the last 90 days.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const dateOrNull = (v?: string) => (v && v.length > 0 ? new Date(v).toISOString() : null);
const isoDay = (d: Date) => d.toISOString().slice(0, 10);

const incidentSchema = z.object({
  type: z.string().min(1),
  affectedMedia: z.string().min(1),
  severity: z.string().min(1),
  incidentDate: z.string().min(1, 'An incident date is required'),
  locationId: z.string().optional().or(z.literal('')),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  description: z.string().min(1, 'A description is required').max(3000),
  spillVolume: z.string().max(1000).optional().or(z.literal('')),
  substanceInvolved: z.string().max(500).optional().or(z.literal('')),
  immediateResponseAction: z.string().max(1000).optional().or(z.literal('')),
  reportedById: z.string().min(1, 'A reporter is required'),
  // Edit-only fields
  status: z.string().min(1),
  reportedToEpa: z.boolean(),
  epaNotificationDate: z.string().optional().or(z.literal('')),
  epaReferenceNumber: z.string().max(100).optional().or(z.literal('')),
  investigationFindings: z.string().max(2000).optional().or(z.literal('')),
  correctiveActions: z.string().max(2000).optional().or(z.literal('')),
});
type IncidentForm = z.input<typeof incidentSchema>;

const closeSchema = z.object({
  closedById: z.string().min(1, 'Who closed it is required'),
  closedDate: z.string().min(1, 'A close date is required'),
  correctiveActions: z.string().max(2000).optional().or(z.literal('')),
});
type CloseForm = z.input<typeof closeSchema>;

const monitoringSchema = z.object({
  monitoringType: z.string().min(1),
  locationId: z.string().optional().or(z.literal('')),
  monitoringPoint: z.string().max(200).optional().or(z.literal('')),
  measurementDate: z.string().min(1, 'A measurement date is required'),
  measuredValue: z.coerce.number(),
  unit: z.string().min(1, 'A unit is required').max(30),
  regulatoryLimit: z.coerce.number().optional(),
  actionLevel: z.coerce.number().optional(),
  instrumentUsed: z.string().max(500).optional().or(z.literal('')),
  weatherConditions: z.string().max(200).optional().or(z.literal('')),
  measuredById: z.string().min(1, 'A measurer is required'),
  comments: z.string().max(1000).optional().or(z.literal('')),
});
type MonitoringForm = z.input<typeof monitoringSchema>;

const severityVariant = (v: string): 'default' | 'secondary' | 'destructive' | 'outline' =>
  v === 'Major' || v === 'Catastrophic' ? 'destructive' : v === 'Moderate' ? 'default' : 'secondary';

function IncidentTable({
  items,
  emptyText,
  onEdit,
  onClose,
  onDelete,
}: {
  items: SheEnvironmentalIncidentSummary[];
  emptyText: string;
  onEdit: (i: SheEnvironmentalIncidentSummary) => void;
  onClose: (i: SheEnvironmentalIncidentSummary) => void;
  onDelete: (i: SheEnvironmentalIncidentSummary) => void;
}) {
  if (items.length === 0) {
    return <EmptyState title="Nothing here" description={emptyText} icon={Leaf} />;
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Number</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Media</TableHead>
              <TableHead>Severity</TableHead>
              <TableHead>Date</TableHead>
              <TableHead>Location</TableHead>
              <TableHead>EPA</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="w-[60px]" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((i) => (
              <TableRow key={i.id}>
                <TableCell className="font-mono">{i.incidentNumber}</TableCell>
                <TableCell className="font-medium">{i.typeName}</TableCell>
                <TableCell>{i.affectedMediaName}</TableCell>
                <TableCell>
                  <Badge variant={severityVariant(i.severity)}>{i.severityName}</Badge>
                </TableCell>
                <TableCell>{fmtDate(i.incidentDate)}</TableCell>
                <TableCell>{i.locationName ?? '—'}</TableCell>
                <TableCell>
                  {i.reportedToEpa ? (
                    <Badge variant="outline">Reported</Badge>
                  ) : (
                    <span className="text-muted-foreground text-sm">—</span>
                  )}
                </TableCell>
                <TableCell>
                  <StatusBadge status={i.statusName} />
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
                      {i.status !== 'Closed' && (
                        <>
                          <DropdownMenuItem onClick={() => onEdit(i)}>Edit…</DropdownMenuItem>
                          <DropdownMenuItem onClick={() => onClose(i)}>
                            Close incident…
                          </DropdownMenuItem>
                        </>
                      )}
                      <DropdownMenuItem className="text-red-600" onClick={() => onDelete(i)}>
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
  );
}

export default function EnvironmentalPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [incidentDialogOpen, setIncidentDialogOpen] = useState(false);
  const [editingIncident, setEditingIncident] = useState<{ id: string; number: string } | null>(null);
  const [closing, setClosing] = useState<SheEnvironmentalIncidentSummary | null>(null);
  const [pendingDelete, setPendingDelete] = useState<SheEnvironmentalIncidentSummary | null>(null);
  const [monitoringDialogOpen, setMonitoringDialogOpen] = useState(false);
  const [editingMonitoring, setEditingMonitoring] = useState<SheEnvironmentalMonitoringRecord | null>(null);
  const [pendingMonitoringDelete, setPendingMonitoringDelete] =
    useState<SheEnvironmentalMonitoringRecord | null>(null);
  const [busy, setBusy] = useState(false);

  const from = isoDay(new Date(Date.now() - 90 * 86400000));
  const to = isoDay(new Date(Date.now() + 86400000));

  const { data: recent = [] } = useQuery({
    queryKey: ['hr', 'safety-env', 'incidents', from, to],
    queryFn: () => safetyEnvironmentalService.getIncidentsByDateRange(from, to),
  });
  const { data: open = [] } = useQuery({
    queryKey: ['hr', 'safety-env', 'incidents', 'open'],
    queryFn: () => safetyEnvironmentalService.getOpenIncidents(),
  });
  const { data: epa = [] } = useQuery({
    queryKey: ['hr', 'safety-env', 'incidents', 'epa'],
    queryFn: () => safetyEnvironmentalService.getIncidentsReportedToEpa(),
  });
  const { data: monitoring = [] } = useQuery({
    queryKey: ['hr', 'safety-env', 'monitoring', from, to],
    queryFn: () => safetyEnvironmentalService.getMonitoringByDateRange(from, to),
  });
  const { data: exceedances = [] } = useQuery({
    queryKey: ['hr', 'safety-env', 'monitoring', 'exceedances'],
    queryFn: () => safetyEnvironmentalService.getExceedances(),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const incidentForm = useForm<IncidentForm>({ resolver: zodResolver(incidentSchema) });
  const closeForm = useForm<CloseForm>({ resolver: zodResolver(closeSchema) });
  const monitoringForm = useForm<MonitoringForm>({ resolver: zodResolver(monitoringSchema) });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'safety-env'] });
  const fail = (fallback: string) => (error: any) =>
    toast({ title: 'Error', description: error?.message || fallback, variant: 'destructive' });

  const locationOptions = locations.map((l) => ({ value: l.id, label: l.name }));

  // ── Incident dialogs ──

  const openCreateIncident = () => {
    setEditingIncident(null);
    incidentForm.reset({
      type: 'OilSpill',
      affectedMedia: 'Soil',
      severity: 'Minor',
      incidentDate: isoDay(new Date()),
      locationId: '',
      specificArea: '',
      description: '',
      spillVolume: '',
      substanceInvolved: '',
      immediateResponseAction: '',
      reportedById: '',
      status: 'Reported',
      reportedToEpa: false,
      epaNotificationDate: '',
      epaReferenceNumber: '',
      investigationFindings: '',
      correctiveActions: '',
    });
    setIncidentDialogOpen(true);
  };

  const openEditIncident = async (row: SheEnvironmentalIncidentSummary) => {
    const full = await safetyEnvironmentalService.getIncident(row.id);
    incidentForm.reset({
      type: full.type,
      affectedMedia: full.affectedMedia,
      severity: full.severity,
      incidentDate: full.incidentDate.slice(0, 10),
      locationId: full.locationId ?? '',
      specificArea: full.specificArea ?? '',
      description: full.description,
      spillVolume: full.spillVolume ?? '',
      substanceInvolved: full.substanceInvolved ?? '',
      immediateResponseAction: full.immediateResponseAction ?? '',
      reportedById: full.reportedById,
      status: full.status,
      reportedToEpa: full.reportedToEpa,
      epaNotificationDate: full.epaNotificationDate?.slice(0, 10) ?? '',
      epaReferenceNumber: full.epaReferenceNumber ?? '',
      investigationFindings: full.investigationFindings ?? '',
      correctiveActions: full.correctiveActions ?? '',
    });
    setEditingIncident({ id: full.id, number: full.incidentNumber });
    setIncidentDialogOpen(true);
  };

  const submitIncident = incidentForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = incidentSchema.parse(values);
      if (editingIncident) {
        await safetyEnvironmentalService.updateIncident(editingIncident.id, {
          id: editingIncident.id,
          type: v.type as SheEnvironmentalIncidentType,
          affectedMedia: v.affectedMedia as SheEnvironmentalMedia,
          severity: v.severity as SheIncidentSeverity,
          incidentDate: new Date(v.incidentDate).toISOString(),
          locationId: blank(v.locationId),
          specificArea: blank(v.specificArea),
          description: v.description,
          spillVolume: blank(v.spillVolume),
          substanceInvolved: blank(v.substanceInvolved),
          immediateResponseAction: blank(v.immediateResponseAction),
          reportedToEpa: v.reportedToEpa,
          epaNotificationDate: dateOrNull(v.epaNotificationDate),
          epaReferenceNumber: blank(v.epaReferenceNumber),
          status: v.status as SheEnvironmentalIncidentStatus,
          investigationFindings: blank(v.investigationFindings),
          correctiveActions: blank(v.correctiveActions),
        });
      } else {
        await safetyEnvironmentalService.createIncident({
          type: v.type as SheEnvironmentalIncidentType,
          affectedMedia: v.affectedMedia as SheEnvironmentalMedia,
          severity: v.severity as SheIncidentSeverity,
          incidentDate: new Date(v.incidentDate).toISOString(),
          locationId: blank(v.locationId),
          specificArea: blank(v.specificArea),
          description: v.description,
          spillVolume: blank(v.spillVolume),
          substanceInvolved: blank(v.substanceInvolved),
          immediateResponseAction: blank(v.immediateResponseAction),
          reportedById: v.reportedById,
        });
      }
      await invalidate();
      toast({ title: editingIncident ? 'Incident updated' : 'Incident recorded' });
      setIncidentDialogOpen(false);
    } catch (error: any) {
      fail('Saving the incident failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  const submitClose = closeForm.handleSubmit(async (values) => {
    if (!closing) return;
    setBusy(true);
    try {
      const v = closeSchema.parse(values);
      await safetyEnvironmentalService.closeIncident(closing.id, {
        incidentId: closing.id,
        closedById: v.closedById,
        closedDate: new Date(v.closedDate).toISOString(),
        correctiveActions: blank(v.correctiveActions),
      });
      await invalidate();
      toast({ title: 'Incident closed', description: closing.incidentNumber });
      setClosing(null);
    } catch (error: any) {
      fail('Closing the incident failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  // ── Monitoring dialogs ──

  const openCreateMonitoring = () => {
    setEditingMonitoring(null);
    monitoringForm.reset({
      monitoringType: 'Noise',
      locationId: '',
      monitoringPoint: '',
      measurementDate: isoDay(new Date()),
      measuredValue: undefined as unknown as number,
      unit: '',
      regulatoryLimit: undefined,
      actionLevel: undefined,
      instrumentUsed: '',
      weatherConditions: '',
      measuredById: '',
      comments: '',
    });
    setMonitoringDialogOpen(true);
  };

  const openEditMonitoring = (r: SheEnvironmentalMonitoringRecord) => {
    setEditingMonitoring(r);
    monitoringForm.reset({
      monitoringType: r.monitoringType,
      locationId: r.locationId ?? '',
      monitoringPoint: r.monitoringPoint ?? '',
      measurementDate: r.measurementDate.slice(0, 10),
      measuredValue: r.measuredValue,
      unit: r.unit,
      regulatoryLimit: r.regulatoryLimit ?? undefined,
      actionLevel: r.actionLevel ?? undefined,
      instrumentUsed: r.instrumentUsed ?? '',
      weatherConditions: r.weatherConditions ?? '',
      measuredById: r.measuredById,
      comments: r.comments ?? '',
    });
    setMonitoringDialogOpen(true);
  };

  const submitMonitoring = monitoringForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = monitoringSchema.parse(values);
      const common = {
        monitoringType: v.monitoringType as SheEnvironmentalMonitoringType,
        locationId: blank(v.locationId),
        monitoringPoint: blank(v.monitoringPoint),
        measurementDate: new Date(v.measurementDate).toISOString(),
        measuredValue: v.measuredValue,
        unit: v.unit,
        regulatoryLimit: v.regulatoryLimit ?? null,
        actionLevel: v.actionLevel ?? null,
        instrumentUsed: blank(v.instrumentUsed),
        weatherConditions: blank(v.weatherConditions),
        comments: blank(v.comments),
        documentPath: editingMonitoring?.documentPath ?? null,
      };
      if (editingMonitoring) {
        await safetyEnvironmentalService.updateMonitoringRecord(editingMonitoring.id, {
          id: editingMonitoring.id,
          ...common,
        });
      } else {
        await safetyEnvironmentalService.createMonitoringRecord({
          ...common,
          measuredById: v.measuredById,
        });
      }
      await invalidate();
      toast({ title: editingMonitoring ? 'Reading updated' : 'Reading recorded' });
      setMonitoringDialogOpen(false);
    } catch (error: any) {
      fail('Saving the reading failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  const MonitoringTable = ({ items, emptyText }: { items: SheEnvironmentalMonitoringRecord[]; emptyText: string }) =>
    items.length === 0 ? (
      <EmptyState title="Nothing here" description={emptyText} icon={Leaf} />
    ) : (
      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Record</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Point</TableHead>
                <TableHead>Date</TableHead>
                <TableHead>Reading</TableHead>
                <TableHead>Limits</TableHead>
                <TableHead>Exceedance</TableHead>
                <TableHead>Measured by</TableHead>
                <TableHead className="w-[60px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((r) => (
                <TableRow key={r.id}>
                  <TableCell className="font-mono">{r.recordNumber}</TableCell>
                  <TableCell className="font-medium">{r.monitoringTypeName}</TableCell>
                  <TableCell>
                    {r.locationName ?? '—'}
                    {r.monitoringPoint ? ` · ${r.monitoringPoint}` : ''}
                  </TableCell>
                  <TableCell>{fmtDate(r.measurementDate)}</TableCell>
                  <TableCell>
                    {r.measuredValue} {r.unit}
                  </TableCell>
                  <TableCell className="text-muted-foreground text-sm">
                    {r.actionLevel != null ? `act ${r.actionLevel}` : ''}
                    {r.actionLevel != null && r.regulatoryLimit != null ? ' · ' : ''}
                    {r.regulatoryLimit != null ? `lim ${r.regulatoryLimit}` : ''}
                    {r.actionLevel == null && r.regulatoryLimit == null ? '—' : ''}
                  </TableCell>
                  <TableCell>
                    {r.exceedsLimit ? (
                      <Badge variant="destructive">Over limit</Badge>
                    ) : r.exceedsActionLevel ? (
                      <Badge variant="default">Over action level</Badge>
                    ) : (
                      <span className="text-muted-foreground text-sm">Within</span>
                    )}
                  </TableCell>
                  <TableCell>{r.measuredByName}</TableCell>
                  <TableCell>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" className="h-8 w-8">
                          <MoreHorizontal className="h-4 w-4" />
                          <span className="sr-only">Actions</span>
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onClick={() => openEditMonitoring(r)}>
                          Edit…
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          className="text-red-600"
                          onClick={() => setPendingMonitoringDelete(r)}
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
    );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Environmental"
        description="Environmental incidents with EPA-notification tracking and a guarded close flow, plus monitoring readings — exceedance flags are computed from the limits you enter. EPA reporting is recorded by hand; nothing is submitted automatically."
        backHref="/hr/safety"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={openCreateMonitoring}>
              <Plus className="mr-2 h-4 w-4" /> Record reading
            </Button>
            <Button onClick={openCreateIncident}>
              <Plus className="mr-2 h-4 w-4" /> Report incident
            </Button>
          </div>
        }
      />

      <Tabs defaultValue="incidents">
        <TabsList>
          <TabsTrigger value="incidents">Incidents</TabsTrigger>
          <TabsTrigger value="monitoring">Monitoring</TabsTrigger>
        </TabsList>

        <TabsContent value="incidents" className="mt-4">
          <Tabs defaultValue="open">
            <TabsList>
              <TabsTrigger value="open">Open ({open.length})</TabsTrigger>
              <TabsTrigger value="epa">Reported to EPA ({epa.length})</TabsTrigger>
              <TabsTrigger value="recent">Last 90 days ({recent.length})</TabsTrigger>
            </TabsList>
            <TabsContent value="open" className="mt-4">
              <IncidentTable
                items={open}
                emptyText="No open environmental incidents."
                onEdit={openEditIncident}
                onClose={(i) => {
                  closeForm.reset({
                    closedById: '',
                    closedDate: isoDay(new Date()),
                    correctiveActions: '',
                  });
                  setClosing(i);
                }}
                onDelete={setPendingDelete}
              />
            </TabsContent>
            <TabsContent value="epa" className="mt-4">
              <IncidentTable
                items={epa}
                emptyText="No incident has been reported to the EPA."
                onEdit={openEditIncident}
                onClose={(i) => {
                  closeForm.reset({
                    closedById: '',
                    closedDate: isoDay(new Date()),
                    correctiveActions: '',
                  });
                  setClosing(i);
                }}
                onDelete={setPendingDelete}
              />
            </TabsContent>
            <TabsContent value="recent" className="mt-4">
              <IncidentTable
                items={recent}
                emptyText="No environmental incidents recorded in the last 90 days."
                onEdit={openEditIncident}
                onClose={(i) => {
                  closeForm.reset({
                    closedById: '',
                    closedDate: isoDay(new Date()),
                    correctiveActions: '',
                  });
                  setClosing(i);
                }}
                onDelete={setPendingDelete}
              />
            </TabsContent>
          </Tabs>
        </TabsContent>

        <TabsContent value="monitoring" className="mt-4">
          <Tabs defaultValue="recent">
            <TabsList>
              <TabsTrigger value="recent">Last 90 days ({monitoring.length})</TabsTrigger>
              <TabsTrigger value="exceedances">Exceedances ({exceedances.length})</TabsTrigger>
            </TabsList>
            <TabsContent value="recent" className="mt-4">
              <MonitoringTable
                items={monitoring}
                emptyText="No readings recorded in the last 90 days."
              />
            </TabsContent>
            <TabsContent value="exceedances" className="mt-4">
              <MonitoringTable
                items={exceedances}
                emptyText="No reading has exceeded a regulatory limit or action level."
              />
            </TabsContent>
          </Tabs>
        </TabsContent>
      </Tabs>

      {/* ── Incident dialog (full record — table rows are summaries) ── */}
      <Dialog open={incidentDialogOpen} onOpenChange={(o) => !busy && setIncidentDialogOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[680px]">
          <DialogHeader>
            <DialogTitle>
              {editingIncident ? `Edit ${editingIncident.number}` : 'Report environmental incident'}
            </DialogTitle>
            <DialogDescription>
              {editingIncident
                ? 'The number and reporter are fixed. Closing goes through its own dialog.'
                : 'Leave the number to the server — it assigns ENV-YYYY-NNNN.'}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitIncident} className="space-y-4">
            <FieldRow>
              <SelectField
                form={incidentForm}
                name="type"
                label="Incident type"
                required
                options={SHE_ENV_INCIDENT_TYPE_OPTIONS}
              />
              <SelectField
                form={incidentForm}
                name="affectedMedia"
                label="Affected media"
                required
                options={SHE_ENV_MEDIA_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={incidentForm}
                name="severity"
                label="Severity"
                required
                options={SHE_INCIDENT_SEVERITY_OPTIONS}
              />
              <DateField form={incidentForm} name="incidentDate" label="Incident date" required />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={incidentForm}
                name="locationId"
                label="Location"
                allowEmpty
                emptyLabel="Not set"
                options={locationOptions}
              />
              <TextField form={incidentForm} name="specificArea" label="Specific area" />
            </FieldRow>
            <TextareaField form={incidentForm} name="description" label="Description" rows={3} required />
            <FieldRow>
              <TextField form={incidentForm} name="spillVolume" label="Spill volume / extent" />
              <TextField form={incidentForm} name="substanceInvolved" label="Substance involved" />
            </FieldRow>
            <TextareaField
              form={incidentForm}
              name="immediateResponseAction"
              label="Immediate response action"
              rows={2}
            />
            {!editingIncident && (
              <EmployeePickerField form={incidentForm} name="reportedById" label="Reported by" required />
            )}
            {editingIncident && (
              <>
                <FieldRow>
                  <SelectField
                    form={incidentForm}
                    name="status"
                    label="Status (closing has its own dialog)"
                    required
                    options={SHE_ENV_INCIDENT_EDIT_STATUS_OPTIONS}
                  />
                  <div />
                </FieldRow>
                <SwitchField form={incidentForm} name="reportedToEpa" label="Reported to EPA" />
                <FieldRow>
                  <DateField form={incidentForm} name="epaNotificationDate" label="EPA notified on" />
                  <TextField form={incidentForm} name="epaReferenceNumber" label="EPA reference" />
                </FieldRow>
                <TextareaField
                  form={incidentForm}
                  name="investigationFindings"
                  label="Investigation findings"
                  rows={2}
                />
                <TextareaField
                  form={incidentForm}
                  name="correctiveActions"
                  label="Corrective actions"
                  rows={2}
                />
              </>
            )}
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setIncidentDialogOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editingIncident ? 'Save' : 'Report incident'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Close-incident dialog ── */}
      <Dialog open={closing !== null} onOpenChange={(o) => !busy && !o && setClosing(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Close incident {closing?.incidentNumber}</DialogTitle>
            <DialogDescription>
              Closing is final — a closed incident can no longer be edited or re-closed.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitClose} className="space-y-4">
            <EmployeePickerField form={closeForm} name="closedById" label="Closed by" required />
            <DateField form={closeForm} name="closedDate" label="Closed on" required />
            <TextareaField
              form={closeForm}
              name="correctiveActions"
              label="Corrective actions (final)"
              rows={3}
            />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setClosing(null)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Close incident
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Monitoring dialog ── */}
      <Dialog open={monitoringDialogOpen} onOpenChange={(o) => !busy && setMonitoringDialogOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>{editingMonitoring ? 'Edit reading' : 'Record reading'}</DialogTitle>
            <DialogDescription>
              Exceedance flags are computed from the measured value and the limits you enter.
              {editingMonitoring ? ' The number and measurer are fixed.' : ''}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitMonitoring} className="space-y-4">
            <FieldRow>
              <SelectField
                form={monitoringForm}
                name="monitoringType"
                label="Monitoring type"
                required
                options={SHE_ENV_MONITORING_TYPE_OPTIONS}
              />
              <DateField form={monitoringForm} name="measurementDate" label="Measured on" required />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={monitoringForm}
                name="locationId"
                label="Location"
                allowEmpty
                emptyLabel="Not set"
                options={locationOptions}
              />
              <TextField form={monitoringForm} name="monitoringPoint" label="Monitoring point" />
            </FieldRow>
            <FieldRow>
              <NumberField form={monitoringForm} name="measuredValue" label="Measured value" required />
              <TextField form={monitoringForm} name="unit" label="Unit (e.g. dB(A), µg/m³)" required />
            </FieldRow>
            <FieldRow>
              <NumberField form={monitoringForm} name="actionLevel" label="Action level" />
              <NumberField form={monitoringForm} name="regulatoryLimit" label="Regulatory limit" />
            </FieldRow>
            <FieldRow>
              <TextField form={monitoringForm} name="instrumentUsed" label="Instrument used" />
              <TextField form={monitoringForm} name="weatherConditions" label="Weather conditions" />
            </FieldRow>
            {!editingMonitoring && (
              <EmployeePickerField form={monitoringForm} name="measuredById" label="Measured by" required />
            )}
            <TextareaField form={monitoringForm} name="comments" label="Comments" rows={2} />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setMonitoringDialogOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editingMonitoring ? 'Save' : 'Record reading'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(o) => !o && setPendingDelete(null)}
        title={`Remove ${pendingDelete?.incidentNumber}?`}
        description="This removes the environmental incident from the register."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingDelete) return;
          try {
            await safetyEnvironmentalService.removeIncident(pendingDelete.id);
            await invalidate();
            toast({ title: 'Removed', description: pendingDelete.incidentNumber });
          } catch (error: any) {
            fail('Removing failed.')(error);
          } finally {
            setPendingDelete(null);
          }
        }}
      />

      <ConfirmationDialog
        open={pendingMonitoringDelete !== null}
        onOpenChange={(o) => !o && setPendingMonitoringDelete(null)}
        title={`Remove ${pendingMonitoringDelete?.recordNumber}?`}
        description="This removes the monitoring reading."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingMonitoringDelete) return;
          try {
            await safetyEnvironmentalService.removeMonitoringRecord(pendingMonitoringDelete.id);
            await invalidate();
            toast({ title: 'Removed', description: pendingMonitoringDelete.recordNumber });
          } catch (error: any) {
            fail('Removing failed.')(error);
          } finally {
            setPendingMonitoringDelete(null);
          }
        }}
      />
    </div>
  );
}
