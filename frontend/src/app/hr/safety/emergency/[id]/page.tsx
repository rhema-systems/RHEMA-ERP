'use client';

import { useState, type ReactNode } from 'react';
import { useParams } from 'next/navigation';
import { z } from 'zod';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil } from 'lucide-react';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
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
import { safetyEmergencyService } from '@/services/hr/safety-emergency.service';
import { locationService } from '@/services/hr/location.service';
import { SHE_EMERGENCY_TYPE_OPTIONS } from '@/types/hr/safety-equipment';
import type {
  SheAssemblyPoint,
  EmergencyContact,
  EmergencyDrill,
  EmergencyResponseTeamMember,
  SheEmergencyType,
} from '@/types/hr/safety-equipment';

/**
 * Emergency-plan detail: the plan text, an edit dialog, and the four child registers —
 * assembly points, the contact tree, drills (with their debrief fields) and the response team.
 * The plan number is fixed after creation; adding the same employee to the team twice is
 * refused, as is a duplicate drill number.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const dateOrNull = (v?: string) => (v && v.length > 0 ? new Date(v).toISOString() : null);
/** "HH:mm" from an <input type="time"> → "HH:mm:ss" TimeSpan; passes "HH:mm:ss" through. */
const timeOrNull = (v?: string) => (v && v.length > 0 ? (v.length === 5 ? `${v}:00` : v) : null);

const editSchema = z.object({
  planName: z.string().min(1).max(200),
  type: z.string().min(1),
  description: z.string().max(2000).optional().or(z.literal('')),
  procedures: z.string().max(4000).optional().or(z.literal('')),
  locationId: z.string().optional().or(z.literal('')),
  lastReviewed: z.string().min(1),
  nextReviewDate: z.string().min(1),
  planOwnerId: z.string().min(1, 'A plan owner is required'),
  isActive: z.boolean(),
});

type EditForm = z.input<typeof editSchema>;

const assemblyPointSchema = z.object({
  name: z.string().min(1, 'A name is required').max(100),
  description: z.string().max(300).optional().or(z.literal('')),
  locationId: z.string().optional().or(z.literal('')),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  capacity: z.coerce.number().min(1).optional(),
  isActive: z.boolean(),
});

type AssemblyPointForm = z.input<typeof assemblyPointSchema>;

const contactSchema = z.object({
  name: z.string().min(1, 'A name is required').max(200),
  role: z.string().min(1, 'A role is required').max(100),
  primaryPhone: z.string().min(1, 'A phone number is required').max(50),
  alternatePhone: z.string().max(50).optional().or(z.literal('')),
  email: z.string().max(100).optional().or(z.literal('')),
  isExternal: z.boolean(),
  displayOrder: z.coerce.number().min(0),
  isActive: z.boolean(),
});

type ContactForm = z.input<typeof contactSchema>;

const drillSchema = z.object({
  drillNumber: z.string().min(1, 'A drill number is required').max(30),
  drillName: z.string().min(1, 'A name is required').max(200),
  drillDate: z.string().min(1, 'A date is required'),
  drillTime: z.string().optional().or(z.literal('')),
  scenario: z.string().max(1000).optional().or(z.literal('')),
  locationId: z.string().optional().or(z.literal('')),
  wasAnnounced: z.boolean(),
  coordinatorId: z.string().min(1, 'A coordinator is required'),
  nextDrillScheduledDate: z.string().optional().or(z.literal('')),
  // Debrief fields — captured after the drill, on edit.
  participantsCount: z.coerce.number().min(0).optional(),
  evacuationTime: z.string().optional().or(z.literal('')),
  observations: z.string().max(2000).optional().or(z.literal('')),
  strengthsIdentified: z.string().max(1000).optional().or(z.literal('')),
  areasForImprovement: z.string().max(1000).optional().or(z.literal('')),
  correctiveActions: z.string().max(1000).optional().or(z.literal('')),
  objectivesMet: z.boolean(),
});

type DrillForm = z.input<typeof drillSchema>;

const teamSchema = z.object({
  employeeId: z.string().min(1, 'An employee is required'),
  role: z.string().min(1, 'A role is required').max(100),
  responsibilities: z.string().max(500).optional().or(z.literal('')),
  certificateExpiryDate: z.string().optional().or(z.literal('')),
  isActive: z.boolean(),
});

type TeamForm = z.input<typeof teamSchema>;

function Fact({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div>
      <div className="text-muted-foreground text-xs">{label}</div>
      <div className="text-sm font-medium">{value ?? '—'}</div>
    </div>
  );
}

export default function EmergencyPlanDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [editOpen, setEditOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const { data: plan } = useQuery({
    queryKey: ['hr', 'safety-emergency', 'plans', id],
    queryFn: () => safetyEmergencyService.getPlan(id),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const editForm = useForm<EditForm>({ resolver: zodResolver(editSchema) });

  const openEdit = () => {
    if (!plan) return;
    editForm.reset({
      planName: plan.planName,
      type: plan.type,
      description: plan.description ?? '',
      procedures: plan.procedures ?? '',
      locationId: plan.locationId ?? '',
      lastReviewed: plan.lastReviewed.slice(0, 10),
      nextReviewDate: plan.nextReviewDate.slice(0, 10),
      planOwnerId: plan.planOwnerId,
      isActive: plan.isActive,
    });
    setEditOpen(true);
  };

  const submitEdit = editForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = editSchema.parse(values);
      await safetyEmergencyService.updatePlan(id, {
        id,
        planName: v.planName,
        type: v.type as SheEmergencyType,
        description: v.description || '',
        procedures: v.procedures || '',
        locationId: blank(v.locationId),
        lastReviewed: new Date(v.lastReviewed).toISOString(),
        nextReviewDate: new Date(v.nextReviewDate).toISOString(),
        planOwnerId: v.planOwnerId,
        isActive: v.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-emergency'] });
      toast({ title: 'Plan updated' });
      setEditOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Updating failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  if (!plan) return null;

  const planKey = ['hr', 'safety-emergency', 'plans', id];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${plan.planNumber} — ${plan.planName}`}
        description={
          SHE_EMERGENCY_TYPE_OPTIONS.find((o) => o.value === plan.type)?.label ?? plan.typeName
        }
        backHref="/hr/safety/emergency"
        actions={
          <Button variant="outline" onClick={openEdit}>
            <Pencil className="mr-2 h-4 w-4" /> Edit
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-3 text-base">
            <StatusBadge status={plan.isActive ? 'Active' : 'Inactive'} />
            {new Date(plan.nextReviewDate) < new Date() && plan.isActive && (
              <Badge variant="destructive">Review overdue</Badge>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
            <Fact label="Location" value={plan.locationName ?? 'All sites'} />
            <Fact label="Plan owner" value={plan.planOwnerName} />
            <Fact label="Last reviewed" value={fmtDate(plan.lastReviewed)} />
            <Fact label="Next review" value={fmtDate(plan.nextReviewDate)} />
          </div>
          {plan.description && <p className="text-sm">{plan.description}</p>}
          {plan.procedures && (
            <div>
              <div className="text-muted-foreground mb-1 text-xs">Procedures</div>
              <p className="whitespace-pre-wrap text-sm">{plan.procedures}</p>
            </div>
          )}
        </CardContent>
      </Card>

      <Tabs defaultValue="assembly-points">
        <TabsList>
          <TabsTrigger value="assembly-points">
            Assembly points ({plan.assemblyPoints.length})
          </TabsTrigger>
          <TabsTrigger value="contacts">Contacts ({plan.emergencyContacts.length})</TabsTrigger>
          <TabsTrigger value="drills">Drills ({plan.drills.length})</TabsTrigger>
          <TabsTrigger value="team">Response team ({plan.teamMembers.length})</TabsTrigger>
        </TabsList>

        {/* ── Assembly points ── */}
        <TabsContent value="assembly-points" className="mt-4">
          <ResourceCollectionTab<SheAssemblyPoint, AssemblyPointForm>
            parentId={id}
            title="assembly points"
            singular="assembly point"
            queryKey={['hr', 'safety-emergency', 'plans', id, 'assembly-points']}
            invalidateKeys={[planKey]}
            list={async () => (await safetyEmergencyService.getPlan(id)).assemblyPoints}
            create={(planId, values) => {
              const v = assemblyPointSchema.parse(values);
              return safetyEmergencyService.addAssemblyPoint(planId, {
                emergencyPlanId: planId,
                name: v.name,
                description: v.description || '',
                locationId: blank(v.locationId),
                specificArea: blank(v.specificArea),
                capacity: v.capacity ?? null,
                isActive: v.isActive,
              });
            }}
            update={(_planId, pointId, values) => {
              const v = assemblyPointSchema.parse(values);
              return safetyEmergencyService.updateAssemblyPoint(pointId, {
                id: pointId,
                name: v.name,
                description: v.description || '',
                locationId: blank(v.locationId),
                specificArea: blank(v.specificArea),
                capacity: v.capacity ?? null,
                isActive: v.isActive,
              });
            }}
            remove={(_planId, pointId) => safetyEmergencyService.removeAssemblyPoint(pointId)}
            columns={[
              { header: 'Name', cell: (a) => <span className="font-medium">{a.name}</span> },
              {
                header: 'Where',
                cell: (a) =>
                  `${a.locationName ?? '—'}${a.specificArea ? ` · ${a.specificArea}` : ''}`,
              },
              { header: 'Capacity', cell: (a) => a.capacity ?? '—' },
              {
                header: 'Status',
                cell: (a) => <StatusBadge status={a.isActive ? 'Active' : 'Inactive'} />,
              },
            ]}
            schema={assemblyPointSchema}
            emptyForm={{
              name: '',
              description: '',
              locationId: '',
              specificArea: '',
              capacity: undefined,
              isActive: true,
            }}
            toForm={(a) => ({
              name: a.name,
              description: a.description ?? '',
              locationId: a.locationId ?? '',
              specificArea: a.specificArea ?? '',
              capacity: a.capacity ?? undefined,
              isActive: a.isActive,
            })}
            renderFields={(f) => (
              <>
                <TextField form={f} name="name" label="Name" required />
                <TextareaField form={f} name="description" label="Description" rows={2} />
                <FieldRow>
                  <SelectField
                    form={f}
                    name="locationId"
                    label="Location"
                    allowEmpty
                    emptyLabel="Not set"
                    options={locations.map((l) => ({ value: l.id, label: l.name }))}
                  />
                  <TextField form={f} name="specificArea" label="Specific area" />
                </FieldRow>
                <FieldRow>
                  <NumberField form={f} name="capacity" label="Capacity (people)" />
                  <div />
                </FieldRow>
                <SwitchField form={f} name="isActive" label="Active" />
              </>
            )}
            getId={(a) => a.id}
            emptyDescription="Where people gather when this plan activates."
          />
        </TabsContent>

        {/* ── Contacts ── */}
        <TabsContent value="contacts" className="mt-4">
          <ResourceCollectionTab<EmergencyContact, ContactForm>
            parentId={id}
            title="emergency contacts"
            singular="contact"
            queryKey={['hr', 'safety-emergency', 'plans', id, 'contacts']}
            invalidateKeys={[planKey]}
            list={async () =>
              (await safetyEmergencyService.getPlan(id)).emergencyContacts.sort(
                (a, b) => a.displayOrder - b.displayOrder,
              )
            }
            create={(planId, values) => {
              const v = contactSchema.parse(values);
              return safetyEmergencyService.addContact(planId, {
                emergencyPlanId: planId,
                name: v.name,
                role: v.role,
                primaryPhone: v.primaryPhone,
                alternatePhone: blank(v.alternatePhone),
                email: blank(v.email),
                isExternal: v.isExternal,
                displayOrder: v.displayOrder,
                isActive: v.isActive,
              });
            }}
            update={(_planId, contactId, values) => {
              const v = contactSchema.parse(values);
              return safetyEmergencyService.updateContact(contactId, {
                id: contactId,
                name: v.name,
                role: v.role,
                primaryPhone: v.primaryPhone,
                alternatePhone: blank(v.alternatePhone),
                email: blank(v.email),
                isExternal: v.isExternal,
                displayOrder: v.displayOrder,
                isActive: v.isActive,
              });
            }}
            remove={(_planId, contactId) => safetyEmergencyService.removeContact(contactId)}
            columns={[
              { header: '#', cell: (c) => c.displayOrder },
              { header: 'Name', cell: (c) => <span className="font-medium">{c.name}</span> },
              { header: 'Role', cell: (c) => c.role },
              {
                header: 'Phone',
                cell: (c) =>
                  `${c.primaryPhone}${c.alternatePhone ? ` / ${c.alternatePhone}` : ''}`,
              },
              {
                header: 'Scope',
                cell: (c) =>
                  c.isExternal ? (
                    <Badge variant="outline">External</Badge>
                  ) : (
                    <Badge variant="secondary">Internal</Badge>
                  ),
              },
            ]}
            schema={contactSchema}
            emptyForm={{
              name: '',
              role: '',
              primaryPhone: '',
              alternatePhone: '',
              email: '',
              isExternal: false,
              displayOrder: 1,
              isActive: true,
            }}
            toForm={(c) => ({
              name: c.name,
              role: c.role,
              primaryPhone: c.primaryPhone,
              alternatePhone: c.alternatePhone ?? '',
              email: c.email ?? '',
              isExternal: c.isExternal,
              displayOrder: c.displayOrder,
              isActive: c.isActive,
            })}
            renderFields={(f) => (
              <>
                <FieldRow>
                  <TextField form={f} name="name" label="Name" required />
                  <TextField form={f} name="role" label="Role (e.g. GNFS, Site Warden)" required />
                </FieldRow>
                <FieldRow>
                  <TextField form={f} name="primaryPhone" label="Primary phone" required />
                  <TextField form={f} name="alternatePhone" label="Alternate phone" />
                </FieldRow>
                <FieldRow>
                  <TextField form={f} name="email" label="Email" />
                  <NumberField form={f} name="displayOrder" label="Display order" />
                </FieldRow>
                <SwitchField
                  form={f}
                  name="isExternal"
                  label="External contact"
                  description="Fire service, ambulance, utility company — anyone outside the organisation."
                />
                <SwitchField form={f} name="isActive" label="Active" />
              </>
            )}
            getId={(c) => c.id}
            emptyDescription="The call tree for this plan, in calling order."
          />
        </TabsContent>

        {/* ── Drills ── */}
        <TabsContent value="drills" className="mt-4">
          <ResourceCollectionTab<EmergencyDrill, DrillForm>
            parentId={id}
            title="drills"
            singular="drill"
            queryKey={['hr', 'safety-emergency', 'plans', id, 'drills']}
            invalidateKeys={[planKey]}
            list={() => safetyEmergencyService.getDrillsForPlan(id)}
            create={(planId, values) => {
              const v = drillSchema.parse(values);
              return safetyEmergencyService.addDrill(planId, {
                emergencyPlanId: planId,
                drillNumber: v.drillNumber,
                drillName: v.drillName,
                drillDate: new Date(v.drillDate).toISOString(),
                drillTime: timeOrNull(v.drillTime),
                scenario: blank(v.scenario),
                locationId: blank(v.locationId),
                wasAnnounced: v.wasAnnounced,
                coordinatorId: v.coordinatorId,
                nextDrillScheduledDate: dateOrNull(v.nextDrillScheduledDate),
              });
            }}
            update={(_planId, drillId, values) => {
              const v = drillSchema.parse(values);
              return safetyEmergencyService.updateDrill(drillId, {
                id: drillId,
                drillName: v.drillName,
                drillDate: new Date(v.drillDate).toISOString(),
                drillTime: timeOrNull(v.drillTime),
                scenario: blank(v.scenario),
                locationId: blank(v.locationId),
                wasAnnounced: v.wasAnnounced,
                participantsCount: v.participantsCount ?? null,
                evacuationTime: timeOrNull(v.evacuationTime),
                observations: blank(v.observations),
                strengthsIdentified: blank(v.strengthsIdentified),
                areasForImprovement: blank(v.areasForImprovement),
                correctiveActions: blank(v.correctiveActions),
                objectivesMet: v.objectivesMet,
                coordinatorId: v.coordinatorId,
                nextDrillScheduledDate: dateOrNull(v.nextDrillScheduledDate),
              });
            }}
            remove={(_planId, drillId) => safetyEmergencyService.removeDrill(drillId)}
            columns={[
              { header: 'Number', cell: (d) => <span className="font-mono">{d.drillNumber}</span> },
              { header: 'Drill', cell: (d) => <span className="font-medium">{d.drillName}</span> },
              { header: 'Date', cell: (d) => fmtDate(d.drillDate) },
              { header: 'Coordinator', cell: (d) => d.coordinatorName },
              { header: 'Participants', cell: (d) => d.participantsCount ?? '—' },
              {
                header: 'Objectives',
                cell: (d) =>
                  d.objectivesMet ? (
                    <Badge variant="secondary">Met</Badge>
                  ) : (
                    <Badge variant="outline">Not met</Badge>
                  ),
              },
              { header: 'Next drill', cell: (d) => fmtDate(d.nextDrillScheduledDate) },
            ]}
            schema={drillSchema}
            emptyForm={{
              drillNumber: '',
              drillName: '',
              drillDate: new Date().toISOString().slice(0, 10),
              drillTime: '',
              scenario: '',
              locationId: '',
              wasAnnounced: true,
              coordinatorId: '',
              nextDrillScheduledDate: '',
              participantsCount: undefined,
              evacuationTime: '',
              observations: '',
              strengthsIdentified: '',
              areasForImprovement: '',
              correctiveActions: '',
              objectivesMet: false,
            }}
            toForm={(d) => ({
              drillNumber: d.drillNumber,
              drillName: d.drillName,
              drillDate: d.drillDate.slice(0, 10),
              drillTime: d.drillTime ?? '',
              scenario: d.scenario ?? '',
              locationId: d.locationId ?? '',
              wasAnnounced: d.wasAnnounced,
              coordinatorId: d.coordinatorId,
              nextDrillScheduledDate: d.nextDrillScheduledDate?.slice(0, 10) ?? '',
              participantsCount: d.participantsCount ?? undefined,
              evacuationTime: d.evacuationTime ?? '',
              observations: d.observations ?? '',
              strengthsIdentified: d.strengthsIdentified ?? '',
              areasForImprovement: d.areasForImprovement ?? '',
              correctiveActions: d.correctiveActions ?? '',
              objectivesMet: d.objectivesMet,
            })}
            renderFields={(f, editing) => (
              <>
                <FieldRow>
                  {!editing ? (
                    <TextField form={f} name="drillNumber" label="Drill number (unique)" required />
                  ) : (
                    <div className="text-muted-foreground self-end pb-2 text-sm">
                      <span className="font-mono">{f.getValues('drillNumber')}</span> — fixed at
                      creation.
                    </div>
                  )}
                  <TextField form={f} name="drillName" label="Drill name" required />
                </FieldRow>
                <FieldRow>
                  <DateField form={f} name="drillDate" label="Drill date" required />
                  <SelectField
                    form={f}
                    name="locationId"
                    label="Location"
                    allowEmpty
                    emptyLabel="Not set"
                    options={locations.map((l) => ({ value: l.id, label: l.name }))}
                  />
                </FieldRow>
                <TextareaField form={f} name="scenario" label="Scenario" rows={2} />
                <FieldRow>
                  <EmployeePickerField form={f} name="coordinatorId" label="Coordinator" required />
                  <DateField form={f} name="nextDrillScheduledDate" label="Next drill scheduled" />
                </FieldRow>
                <SwitchField
                  form={f}
                  name="wasAnnounced"
                  label="Announced drill"
                  description="Unannounced drills test real readiness."
                />
                {editing && (
                  <>
                    <FieldRow>
                      <NumberField form={f} name="participantsCount" label="Participants" />
                      <SwitchField form={f} name="objectivesMet" label="Objectives met" />
                    </FieldRow>
                    <TextareaField form={f} name="observations" label="Observations" rows={2} />
                    <TextareaField form={f} name="strengthsIdentified" label="Strengths" rows={2} />
                    <TextareaField
                      form={f}
                      name="areasForImprovement"
                      label="Areas for improvement"
                      rows={2}
                    />
                    <TextareaField
                      form={f}
                      name="correctiveActions"
                      label="Corrective actions"
                      rows={2}
                    />
                  </>
                )}
              </>
            )}
            getId={(d) => d.id}
            dialogClassName="sm:max-w-[640px]"
            emptyDescription="Drills run against this plan. The debrief (participants, observations, outcomes) is captured by editing the drill afterwards."
          />
        </TabsContent>

        {/* ── Response team ── */}
        <TabsContent value="team" className="mt-4">
          <ResourceCollectionTab<EmergencyResponseTeamMember, TeamForm>
            parentId={id}
            title="response team"
            singular="team member"
            queryKey={['hr', 'safety-emergency', 'plans', id, 'team']}
            invalidateKeys={[planKey]}
            list={async () => (await safetyEmergencyService.getPlan(id)).teamMembers}
            create={(planId, values) => {
              const v = teamSchema.parse(values);
              return safetyEmergencyService.addTeamMember(planId, {
                emergencyPlanId: planId,
                employeeId: v.employeeId,
                role: v.role,
                responsibilities: blank(v.responsibilities),
                certificateExpiryDate: dateOrNull(v.certificateExpiryDate),
                isActive: v.isActive,
              });
            }}
            update={(_planId, memberId, values) => {
              const v = teamSchema.parse(values);
              return safetyEmergencyService.updateTeamMember(memberId, {
                id: memberId,
                role: v.role,
                responsibilities: blank(v.responsibilities),
                certificateExpiryDate: dateOrNull(v.certificateExpiryDate),
                isActive: v.isActive,
              });
            }}
            remove={(_planId, memberId) => safetyEmergencyService.removeTeamMember(memberId)}
            columns={[
              { header: 'Employee', cell: (t) => <span className="font-medium">{t.employeeName}</span> },
              { header: 'Role', cell: (t) => t.role },
              {
                header: 'Certificate expiry',
                cell: (t) =>
                  t.certificateExpiryDate && new Date(t.certificateExpiryDate) < new Date() ? (
                    <Badge variant="destructive">Expired {fmtDate(t.certificateExpiryDate)}</Badge>
                  ) : (
                    fmtDate(t.certificateExpiryDate)
                  ),
              },
              {
                header: 'Status',
                cell: (t) => <StatusBadge status={t.isActive ? 'Active' : 'Inactive'} />,
              },
            ]}
            schema={teamSchema}
            emptyForm={{
              employeeId: '',
              role: '',
              responsibilities: '',
              certificateExpiryDate: '',
              isActive: true,
            }}
            toForm={(t) => ({
              employeeId: t.employeeId,
              role: t.role,
              responsibilities: t.responsibilities ?? '',
              certificateExpiryDate: t.certificateExpiryDate?.slice(0, 10) ?? '',
              isActive: t.isActive,
            })}
            renderFields={(f, editing) => (
              <>
                {!editing && (
                  <EmployeePickerField form={f} name="employeeId" label="Employee" required />
                )}
                <TextField form={f} name="role" label="Role (e.g. Fire Warden, First Aider)" required />
                <TextareaField form={f} name="responsibilities" label="Responsibilities" rows={2} />
                <DateField form={f} name="certificateExpiryDate" label="Certificate expiry" />
                <SwitchField form={f} name="isActive" label="Active" />
              </>
            )}
            getId={(t) => t.id}
            emptyDescription="Who responds when this plan activates. The same employee cannot be added twice."
          />
        </TabsContent>
      </Tabs>

      {/* ── Edit dialog ── */}
      <Dialog open={editOpen} onOpenChange={(o) => !busy && setEditOpen(o)}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit plan</DialogTitle>
            <DialogDescription>
              {plan.planNumber} — the number itself cannot change.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitEdit} className="space-y-4">
            <FieldRow>
              <TextField form={editForm} name="planName" label="Plan name" required />
              <SelectField
                form={editForm}
                name="type"
                label="Emergency type"
                required
                options={SHE_EMERGENCY_TYPE_OPTIONS}
              />
            </FieldRow>
            <TextareaField form={editForm} name="description" label="Description" rows={2} />
            <TextareaField form={editForm} name="procedures" label="Procedures" rows={5} />
            <FieldRow>
              <SelectField
                form={editForm}
                name="locationId"
                label="Location"
                allowEmpty
                emptyLabel="All sites"
                options={locations.map((l) => ({ value: l.id, label: l.name }))}
              />
              <EmployeePickerField
                form={editForm}
                name="planOwnerId"
                label="Plan owner"
                required
                initialLabel={plan.planOwnerName}
              />
            </FieldRow>
            <FieldRow>
              <DateField form={editForm} name="lastReviewed" label="Last reviewed" required />
              <DateField form={editForm} name="nextReviewDate" label="Next review due" required />
            </FieldRow>
            <SwitchField form={editForm} name="isActive" label="Active" />
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
    </div>
  );
}
