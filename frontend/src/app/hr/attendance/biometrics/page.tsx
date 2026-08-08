'use client';

import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Fingerprint } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { employeeBiometricService } from '@/services/hr/attendance-setup.service';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { BIOMETRIC_TYPE_OPTIONS, FINGER_POSITION_OPTIONS } from '@/types/hr/attendance';
import type { EmployeeBiometricSummary } from '@/types/hr/attendance';

/**
 * Enrolled biometric templates, per employee.
 *
 * The template itself (`biometricData`) is an opaque vendor blob — it is written on
 * enrolment and never read back into this screen. Revoking deactivates a template while
 * keeping the audit trail; deleting removes it outright, which is only appropriate when the
 * enrolment was a mistake.
 */
const biometricSchema = z.object({
  biometricType: z.enum(['Fingerprint', 'Face', 'Iris', 'Palm', 'Vein', 'Retina']),
  bodyPart: z.string().max(100).optional(),
  fingerPosition: z.string().optional(),
  biometricData: z.string().min(1, 'The template data is required').max(2000),
  templateFormat: z.string().max(100).optional(),
  qualityScore: z.coerce.number().min(0).max(100).optional(),
  deviceId: z.string().max(100).optional(),
  deviceModel: z.string().max(200).optional(),
});

type BiometricForm = z.input<typeof biometricSchema>;

const emptyBiometric: BiometricForm = {
  biometricType: 'Fingerprint',
  bodyPart: '',
  fingerPosition: '',
  biometricData: '',
  templateFormat: '',
  qualityScore: undefined,
  deviceId: '',
  deviceModel: '',
};

export default function BiometricsPage() {
  const queryClient = useQueryClient();
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [employeeLabel, setEmployeeLabel] = useState<string | null>(null);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Employee Biometrics"
        description="Fingerprint, face and other templates that let devices recognise an employee."
        backHref="/hr/attendance"
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Employee</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="max-w-md">
            <EmployeePicker
              value={employeeId}
              initialLabel={employeeLabel}
              onChange={(id, label) => {
                setEmployeeId(id);
                setEmployeeLabel(label);
              }}
            />
          </div>
        </CardContent>
      </Card>

      {!employeeId ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={Fingerprint}
              title="Choose an employee"
              description="Templates are enrolled and listed per employee."
            />
          </CardContent>
        </Card>
      ) : (
        <ResourceCollectionTab<EmployeeBiometricSummary, BiometricForm>
          parentId={employeeId}
          title="templates"
          singular="template"
          queryKey={['hr', 'biometrics', employeeId]}
          dialogHint="Capture the template on the enrolment device, then paste the encoded value here."
          emptyDescription="This employee has no enrolled templates, so devices cannot recognise them."
          // There is no update endpoint that makes sense from this screen — a template is
          // re-enrolled rather than edited — so rows are add/revoke/remove only.
          allowUpdate={false}
          list={(id) => employeeBiometricService.getByEmployee(id)}
          create={(id, values) => {
            const v = biometricSchema.parse(values);
            return employeeBiometricService.enrol({
              employeeId: id,
              biometricType: v.biometricType,
              bodyPart: v.bodyPart || null,
              fingerPosition: (v.fingerPosition || null) as any,
              biometricData: v.biometricData,
              templateFormat: v.templateFormat || null,
              qualityScore: v.qualityScore ?? null,
              deviceId: v.deviceId || null,
              deviceModel: v.deviceModel || null,
              enrolledById: null,
            });
          }}
          update={async () => undefined}
          remove={(_id, biometricId) => employeeBiometricService.remove(biometricId)}
          getId={(b) => b.id}
          actions={[
            {
              label: 'Revoke',
              visible: (b) => b.isActive,
              destructive: true,
              run: async (b) => {
                await employeeBiometricService.revoke(b.id, 'Revoked from the biometrics screen');
                await queryClient.invalidateQueries({ queryKey: ['hr', 'biometrics', employeeId] });
              },
              confirm: {
                title: 'Revoke this template?',
                description:
                  'Deactivates it so devices stop matching against it, while keeping the enrolment history.',
              },
            },
          ]}
          columns={[
            {
              header: 'Type',
              cell: (b) => <span className="font-medium">{humanizeEnum(b.biometricType)}</span>,
            },
            {
              header: 'Position',
              cell: (b) => (b.fingerPosition ? humanizeEnum(b.fingerPosition) : b.bodyPart || '—'),
            },
            {
              header: 'Quality',
              cell: (b) => (b.qualityScore != null ? `${b.qualityScore}%` : '—'),
              className: 'text-right',
            },
            { header: 'Enrolled', cell: (b) => formatDate(b.enrolledDate) },
            { header: 'Status', cell: (b) => <StatusBadge active={b.isActive} /> },
          ]}
          schema={biometricSchema as any}
          emptyForm={emptyBiometric}
          toForm={() => emptyBiometric}
          renderFields={(form) => {
            const isFingerprint = form.watch('biometricType') === 'Fingerprint';
            return (
              <>
                <SelectField
                  form={form}
                  name="biometricType"
                  label="Biometric type"
                  required
                  options={BIOMETRIC_TYPE_OPTIONS}
                />
                {isFingerprint ? (
                  <SelectField
                    form={form}
                    name="fingerPosition"
                    label="Finger"
                    options={FINGER_POSITION_OPTIONS}
                    allowEmpty
                  />
                ) : (
                  <TextField
                    form={form}
                    name="bodyPart"
                    label="Body part"
                    placeholder="e.g. Left iris"
                  />
                )}
                <TextareaField
                  form={form}
                  name="biometricData"
                  label="Template data"
                  rows={4}
                  placeholder="The encoded template from the enrolment device"
                />
                <FieldRow>
                  <TextField
                    form={form}
                    name="templateFormat"
                    label="Template format"
                    placeholder="e.g. ISO 19794-2"
                  />
                  <NumberField form={form} name="qualityScore" label="Quality score (0–100)" />
                </FieldRow>
                <FieldRow>
                  <TextField form={form} name="deviceId" label="Enrolment device ID" />
                  <TextField form={form} name="deviceModel" label="Device model" />
                </FieldRow>
              </>
            );
          }}
        />
      )}
    </div>
  );
}
