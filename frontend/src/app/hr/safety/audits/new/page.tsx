'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { z } from 'zod';
import { useForm } from 'react-hook-form';
import { useQuery } from '@tanstack/react-query';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  TextareaField,
  SelectField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyAuditService } from '@/services/hr/safety-audit.service';
import { locationService } from '@/services/hr/location.service';
import { SHE_AUDIT_TYPE_OPTIONS, type SheAuditType } from '@/types/hr/safety-audits';

/**
 * Plans a SHE audit. The audit number is server-assigned (AUD-YYYY-NNNN) unless one
 * is supplied; findings, team and the lifecycle live on the detail page. New audits
 * start as Planned.
 */
const auditSchema = z.object({
  auditNumber: z.string().max(30).optional().or(z.literal('')),
  title: z.string().min(1, 'A title is required').max(200),
  type: z.string().min(1, 'A type is required'),
  standard: z.string().max(200).optional().or(z.literal('')),
  scope: z.string().max(1000).optional().or(z.literal('')),
  objectives: z.string().max(1000).optional().or(z.literal('')),
  locationId: z.string().optional().or(z.literal('')),
  leadAuditorId: z.string().min(1, 'A lead auditor is required'),
  externalAuditorName: z.string().max(200).optional().or(z.literal('')),
  externalAuditorOrganization: z.string().max(200).optional().or(z.literal('')),
  plannedStartDate: z.string().min(1, 'A planned start is required'),
  plannedEndDate: z.string().optional().or(z.literal('')),
});

type AuditForm = z.input<typeof auditSchema>;

const blank = (v?: string) => (v && v.length > 0 ? v : null);
const dateOrNull = (v?: string) => (v && v.length > 0 ? new Date(v).toISOString() : null);

export default function NewSheAuditPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);

  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const form = useForm<AuditForm>({
    resolver: zodResolver(auditSchema),
    defaultValues: {
      auditNumber: '',
      title: '',
      type: 'Internal',
      standard: '',
      scope: '',
      objectives: '',
      locationId: '',
      leadAuditorId: '',
      externalAuditorName: '',
      externalAuditorOrganization: '',
      plannedStartDate: new Date().toISOString().slice(0, 10),
      plannedEndDate: '',
    },
  });

  const submit = form.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = auditSchema.parse(values);
      const created = await safetyAuditService.create({
        auditNumber: blank(v.auditNumber),
        title: v.title,
        type: v.type as SheAuditType,
        standard: blank(v.standard),
        scope: blank(v.scope),
        objectives: blank(v.objectives),
        locationId: blank(v.locationId),
        leadAuditorId: v.leadAuditorId,
        externalAuditorName: blank(v.externalAuditorName),
        externalAuditorOrganization: blank(v.externalAuditorOrganization),
        plannedStartDate: new Date(v.plannedStartDate).toISOString(),
        plannedEndDate: dateOrNull(v.plannedEndDate),
      });
      toast({ title: 'Audit planned', description: `${created.auditNumber} starts as Planned.` });
      router.push(`/hr/safety/audits/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Planning the audit failed.',
        variant: 'destructive',
      });
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Plan SHE Audit"
        description="Number is server-assigned unless supplied. Start the audit, record findings and issue the report from the detail page."
        backHref="/hr/safety/audits"
      />

      <form onSubmit={submit}>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Audit plan</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField form={form} name="title" label="Title" required />
              <SelectField form={form} name="type" label="Type" required options={SHE_AUDIT_TYPE_OPTIONS} />
            </FieldRow>
            <FieldRow>
              <TextField
                form={form}
                name="standard"
                label="Standard / criteria (e.g. ISO 45001:2018)"
              />
              <TextField form={form} name="auditNumber" label="Audit number (blank = assigned)" />
            </FieldRow>
            <TextareaField form={form} name="scope" label="Scope" rows={2} />
            <TextareaField form={form} name="objectives" label="Objectives" rows={2} />
            <FieldRow>
              <SelectField
                form={form}
                name="locationId"
                label="Location"
                options={locations.map((l) => ({ value: l.id, label: l.name }))}
              />
              <EmployeePickerField form={form} name="leadAuditorId" label="Lead auditor (internal coordinator)" required />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="externalAuditorName" label="External auditor (if any)" />
              <TextField form={form} name="externalAuditorOrganization" label="External auditor's organisation" />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="plannedStartDate" label="Planned start" required />
              <DateField form={form} name="plannedEndDate" label="Planned end" />
            </FieldRow>
            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => router.back()} disabled={busy}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                Plan audit
              </Button>
            </div>
          </CardContent>
        </Card>
      </form>
    </div>
  );
}
