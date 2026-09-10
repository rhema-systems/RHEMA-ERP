'use client';

import { useState } from 'react';
import { z } from 'zod';
import { useQueryClient } from '@tanstack/react-query';
import { Paperclip } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useToast } from '@/hooks/use-toast';
import { AttachFileDialog } from '@/components/hr/common/AttachFileDialog';
import { employeeService } from '@/services/hr/employee.service';
import { employeeDocumentService } from '@/services/hr/employee-document.service';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import { REFEREE_TYPE_OPTIONS, type EmployeeReferee } from '@/types/hr/employee-subresources';
import { RELATIONSHIP_SCOPES } from '@/types/hr/relationship-type';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { RelationshipField } from './address-fields';
import { FieldRow, SelectField, SwitchField, TextField } from './fields';

const schema = z.object({
  refereeType: z.enum(['Professional', 'Academic', 'Personal']),
  fullName: z.string().min(1, 'Name is required').max(100),
  organization: z.string().max(200).optional().or(z.literal('')),
  positionOrTitle: z.string().max(100).optional().or(z.literal('')),
  relationship: z.string().min(1, 'Relationship is required').max(200),
  relationshipTypeId: z.string().optional().or(z.literal('')),
  phoneNumber: z.string().min(1, 'Phone number is required').max(50),
  emailAddress: z.string().email('Enter a valid email').optional().or(z.literal('')),
  isPrimary: z.boolean(),
  isActive: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  refereeType: 'Professional',
  fullName: '',
  organization: '',
  positionOrTitle: '',
  relationship: '',
  relationshipTypeId: '',
  phoneNumber: '',
  emailAddress: '',
  isPrimary: false,
  isActive: true,
};

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  refereeType: v.refereeType,
  fullName: v.fullName,
  organization: v.organization || null,
  positionOrTitle: v.positionOrTitle || null,
  relationship: v.relationship,
  // ⚠ When an id is sent the server OVERWRITES `relationship` with the catalogue row's name.
  relationshipTypeId: v.relationshipTypeId || null,
  // ⚠ Nulls mean "not supplied" on the referee UPDATE DTO, so unlinking has to say so explicitly.
  // The tab sends the whole form every save, and without this an emptied dropdown would save
  // successfully and change nothing.
  clearRelationshipType: !v.relationshipTypeId,
  phoneNumber: v.phoneNumber,
  emailAddress: v.emailAddress || null,
  isPrimary: v.isPrimary,
  isActive: v.isActive,
});

/**
 * Referees, and the written reference each one sent (demo feedback round 2, E-10).
 *
 * ⚠ The letter's upload and download routes existed since lane 3a with no caller in the
 * frontend — the backend was verified, the product was not. The paperclip column and the
 * "Reference letter…" action are that caller.
 */
export function RefereesTab({ employeeId }: { employeeId: string }) {
  const qc = useQueryClient();
  const { toast } = useToast();
  const [letterFor, setLetterFor] = useState<EmployeeReferee | null>(null);

  const refresh = () =>
    qc.invalidateQueries({ queryKey: ['hr', 'employees', employeeId, 'referees'] });

  const downloadLetter = async (r: EmployeeReferee) => {
    try {
      await hrDocumentService.download(
        employeeDocumentService.refereeLetterUrl(r.id), r.letterFileName ?? 'reference-letter');
    } catch {
      toast({ variant: 'destructive', title: 'Could not download that letter' });
    }
  };

  return (
    <>
      <EmployeeSubResourceTab<EmployeeReferee, FormValues>
        employeeId={employeeId}
        title="referees"
        singular="referee"
        queryKey="referees"
        getId={(r) => r.id}
        list={employeeService.getReferees.bind(employeeService)}
        create={(id, v) => employeeService.addReferee(id, toPayload(id, v))}
        update={(id, refereeId, v) =>
          employeeService.updateReferee(id, refereeId, { id: refereeId, ...toPayload(id, v) })
        }
        remove={employeeService.removeReferee.bind(employeeService)}
        actions={[
          {
            label: (r) => (r.hasLetter ? 'Reference letter…' : 'Attach reference letter…'),
            run: async (r) => setLetterFor(r),
          },
          {
            label: 'Set as primary',
            visible: (r) => !r.isPrimary,
            run: (r) => employeeService.setPrimaryReferee(employeeId, r.id),
          },
          {
            label: (r) => (r.isActive ? 'Deactivate' : 'Activate'),
            run: (r) =>
              r.isActive
                ? employeeService.deactivateReferee(employeeId, r.id)
                : employeeService.activateReferee(employeeId, r.id),
          },
        ]}
        columns={[
          { header: 'Name', cell: (r) => r.fullName },
          { header: 'Type', cell: (r) => r.refereeType },
          { header: 'Organization', cell: (r) => r.organization || '—' },
          { header: 'Relationship', cell: (r) => r.relationship },
          { header: 'Phone', cell: (r) => r.phoneNumber },
          {
            header: 'Letter',
            cell: (r) =>
              r.hasLetter ? (
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-7 gap-1 px-2"
                  title={r.letterFileName ?? 'Download the reference letter'}
                  onClick={(e) => { e.stopPropagation(); void downloadLetter(r); }}
                >
                  <Paperclip className="h-3.5 w-3.5" />
                  <span className="max-w-[10rem] truncate text-xs">{r.letterFileName ?? 'On file'}</span>
                </Button>
              ) : (
                <span className="text-xs text-muted-foreground">—</span>
              ),
          },
          {
            header: 'Status',
            cell: (r) => (
              <div className="flex gap-1">
                {r.isPrimary && <Badge variant="secondary">Primary</Badge>}
                {r.isContacted && <Badge variant="outline">Contacted</Badge>}
                {!r.isActive && <Badge variant="outline">Inactive</Badge>}
              </div>
            ),
          },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={(r) => ({
          refereeType: r.refereeType,
          fullName: r.fullName,
          organization: r.organization ?? '',
          positionOrTitle: r.positionOrTitle ?? '',
          relationship: r.relationship,
          relationshipTypeId: r.relationshipTypeId ?? '',
          phoneNumber: r.phoneNumber,
          emailAddress: r.emailAddress ?? '',
          isPrimary: r.isPrimary,
          isActive: r.isActive,
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="fullName" label="Full name" required />
              <SelectField
                form={form}
                name="refereeType"
                label="Referee type"
                required
                options={REFEREE_TYPE_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="organization" label="Organization" />
              <TextField form={form} name="positionOrTitle" label="Position / title" />
            </FieldRow>
            {/*
              ⚠ The accepted set depends on the referee TYPE, watched live: a personal referee may
              be a relative or a family friend; a professional or academic one may not. Switching
              the type clears a selection the new kind does not accept — see RelationshipField.
            */}
            <RelationshipField
              form={form}
              typeIdName="relationshipTypeId"
              textName="relationship"
              categories={
                form.watch('refereeType') === 'Personal'
                  ? RELATIONSHIP_SCOPES.personalReferee
                  : RELATIONSHIP_SCOPES.professionalReferee
              }
              required
            />
            <FieldRow>
              <TextField form={form} name="phoneNumber" label="Phone" type="tel" required />
              <TextField form={form} name="emailAddress" label="Email" type="email" />
            </FieldRow>
            <FieldRow>
              <SwitchField form={form} name="isPrimary" label="Primary referee" />
              <SwitchField form={form} name="isActive" label="Active" />
            </FieldRow>
            <p className="text-xs text-muted-foreground">
              The written reference is attached from the row's menu after saving — it goes through
              the document store, not this form.
            </p>
          </>
        )}
      />

      <AttachFileDialog
        open={letterFor !== null}
        onOpenChange={(o) => !o && setLetterFor(null)}
        title={`Reference letter — ${letterFor?.fullName ?? ''}`}
        description="The letter the referee wrote. Receiving it marks the referee as contacted."
        currentFileName={letterFor?.hasLetter ? letterFor.letterFileName : null}
        currentFileSize={letterFor?.letterFileSizeBytes}
        downloadUrl={letterFor ? employeeDocumentService.refereeLetterUrl(letterFor.id) : undefined}
        upload={(file) => employeeDocumentService.uploadRefereeLetter(letterFor!.id, file)}
        onUploaded={() => void refresh()}
      />
    </>
  );
}
