'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { DateField, FieldRow, SwitchField, TextareaField, TextField } from '@/components/hr/employee/tabs/fields';
import { disciplineWitnessService } from '@/services/hr/discipline.service';
import type { DisciplineWitness } from '@/types/hr/discipline';

const toInput = (v?: string | null) => (v ? v.slice(0, 10) : '');
const orNull = (v?: string | null) => {
  const t = (v ?? '').trim();
  return t.length > 0 ? t : null;
};

const schema = z
  .object({
    name: z.string().trim().min(1, 'The witness needs a name').max(200),
    isEmployee: z.boolean(),
    employeeId: z.string().optional(),
    contactInfo: z.string().max(200).optional(),
    statement: z.string().max(4000).optional(),
    statementDate: z.string().optional(),
  })
  .refine((v) => !v.isEmployee || !!v.employeeId, {
    message: 'Pick the employee, or record them as external',
    path: ['employeeId'],
  })
  .refine((v) => !v.statementDate || !!orNull(v.statement), {
    message: 'A statement date with no statement records nothing',
    path: ['statement'],
  });

type Form = z.infer<typeof schema>;

const empty: Form = {
  name: '',
  isEmployee: false,
  employeeId: '',
  contactInfo: '',
  statement: '',
  statementDate: '',
};

/**
 * People who saw what happened, and what they said.
 *
 * ⚠ **`isEmployee` and `employeeId` are separate fields and the server trusts both as given.**
 * Nothing clears the id when the flag goes false, so a row can claim to be an external witness
 * while still pointing at a staff record — and the case detail's summary shows only the flag, so
 * the contradiction would be invisible. This panel keeps the pair consistent in both directions.
 *
 * ⚠ **`contactInfo` was unreachable on both the create and update DTOs** — one of the ledger's
 * section-E gaps. It is the only way to reach an external witness who has not yet given a
 * statement, which is exactly who the `without-statement` read is for.
 *
 * ⚠ **The full record is read here, not taken from the case detail**, whose summary omits
 * `contactInfo` and the employee's name entirely.
 */
export function WitnessesPanel({
  caseId,
  canWrite,
  canDelete,
  onChanged,
}: {
  caseId: string;
  canWrite: boolean;
  canDelete: boolean;
  onChanged?: () => void;
}) {
  const queryKey = ['hr', 'discipline', caseId, 'witnesses'];

  return (
    <Card>
      <CardHeader><CardTitle>Witnesses</CardTitle></CardHeader>
      <CardContent>
        <ResourceCollectionTab<DisciplineWitness, Form>
          parentId={caseId}
          title="witnesses"
          singular="witness"
          queryKey={queryKey}
          readOnly={!canWrite}
          dialogClassName="sm:max-w-[620px]"
          dialogHint="Someone who saw what happened, whether or not they work here."
          emptyDescription="Nobody has been recorded as a witness on this case."
          list={(id) => disciplineWitnessService.getForCase(id)}
          create={(id, v) =>
            disciplineWitnessService
              .add(id, {
                name: v.name.trim(),
                isEmployee: v.isEmployee,
                // Cleared when the witness is external, so the flag and the link cannot disagree.
                employeeId: v.isEmployee ? (v.employeeId || null) : null,
                contactInfo: orNull(v.contactInfo),
                statement: orNull(v.statement),
                statementDate: orNull(v.statementDate),
              })
              .then((r) => { onChanged?.(); return r; })
          }
          update={(_id, witnessId, v) =>
            disciplineWitnessService
              .update(witnessId, {
                name: v.name.trim(),
                isEmployee: v.isEmployee,
                employeeId: v.isEmployee ? (v.employeeId || null) : null,
                contactInfo: orNull(v.contactInfo),
                statement: orNull(v.statement),
                statementDate: orNull(v.statementDate),
              })
              .then((r) => { onChanged?.(); return r; })
          }
          remove={
            canDelete
              ? (_id, witnessId) =>
                  disciplineWitnessService.remove(witnessId).then((r) => { onChanged?.(); return r; })
              : undefined
          }
          getId={(w) => w.id}
          columns={[
            { header: 'Name', cell: (w) => w.employeeName || w.name },
            {
              header: 'Type',
              cell: (w) =>
                w.isEmployee ? <Badge variant="outline">Employee</Badge> : <Badge variant="secondary">External</Badge>,
            },
            {
              header: 'Contact',
              cell: (w) => <span className="text-muted-foreground">{w.contactInfo || '—'}</span>,
            },
            {
              header: 'Statement',
              cell: (w) =>
                w.statement ? (
                  <Badge variant="outline">On file</Badge>
                ) : (
                  <Badge variant="secondary">Outstanding</Badge>
                ),
            },
            { header: 'Dated', cell: (w) => toInput(w.statementDate) || '—' },
          ]}
          schema={schema}
          emptyForm={empty}
          toForm={(w) => ({
            name: w.name,
            isEmployee: w.isEmployee,
            employeeId: w.employeeId ?? '',
            contactInfo: w.contactInfo ?? '',
            statement: w.statement ?? '',
            statementDate: toInput(w.statementDate),
          })}
          renderFields={(form) => {
            const isEmployee = !!form.watch('isEmployee');
            return (
              <>
                <SwitchField
                  form={form}
                  name="isEmployee"
                  label="This witness works here"
                  description="Off records an external witness — a contractor, customer or member of the public."
                />

                {isEmployee ? (
                  <div className="space-y-2">
                    <Label>Employee<span className="ml-0.5 text-red-500">*</span></Label>
                    <EmployeePicker
                      value={form.watch('employeeId') || null}
                      onChange={(id, label) => {
                        form.setValue('employeeId', id ?? '', { shouldValidate: true });
                        // The name column falls back to `name`, so keep it populated either way.
                        if (label && !form.getValues('name')) {
                          form.setValue('name', label, { shouldValidate: true });
                        }
                      }}
                    />
                    {form.formState.errors.employeeId && (
                      <p className="text-sm text-red-500">{form.formState.errors.employeeId.message}</p>
                    )}
                  </div>
                ) : null}

                <FieldRow>
                  <TextField form={form} name="name" label="Name" required />
                  <TextField
                    form={form}
                    name="contactInfo"
                    label="Contact"
                    placeholder={isEmployee ? 'Optional' : 'Phone or email — how to reach them'}
                  />
                </FieldRow>

                <TextareaField
                  form={form}
                  name="statement"
                  label="Statement"
                  rows={5}
                  placeholder="What the witness said. Leave blank until they have given one."
                />
                <DateField form={form} name="statementDate" label="Statement dated" />
              </>
            );
          }}
        />
      </CardContent>
    </Card>
  );
}
