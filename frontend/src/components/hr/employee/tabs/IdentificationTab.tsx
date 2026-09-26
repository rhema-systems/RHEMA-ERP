'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { identificationTypeService } from '@/services/hr/lookup.service';
import { employeeService } from '@/services/hr/employee.service';
import type { EmployeeIdentificationCard } from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { DateField, FieldRow, SelectField, TextField, TextareaField } from './fields';

const schema = z.object({
  identificationTypeId: z.string().min(1, 'Select an identification type'),
  documentNumber: z.string().min(1, 'Document number is required').max(100),
  issueDate: z.string().optional().or(z.literal('')),
  expiryDate: z.string().optional().or(z.literal('')),
  notes: z.string().max(1000).optional().or(z.literal('')),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  identificationTypeId: '',
  documentNumber: '',
  issueDate: '',
  expiryDate: '',
  notes: '',
};

const isExpired = (expiry?: string | null) =>
  !!expiry && new Date(expiry).getTime() < Date.now();

export function IdentificationTab({ employeeId }: { employeeId: string }) {
  const { data: types } = useQuery({
    queryKey: ['hr', 'identification-types', 'active'],
    queryFn: () => identificationTypeService.getActive(),
  });

  const typeOptions = (types ?? []).map((t) => ({ value: t.id, label: t.name }));

  return (
    <EmployeeSubResourceTab<EmployeeIdentificationCard, FormValues>
      employeeId={employeeId}
      title="identification documents"
      singular="identification document"
      itemLabel={(c) => c.identificationTypeName || c.cardTypeName}
      queryKey="identification-cards"
      getId={(c) => c.id}
      list={employeeService.getIdentificationCards.bind(employeeService)}
      create={(id, v) =>
        employeeService.addIdentificationCard(id, {
          employeeId: id,
          identificationTypeId: v.identificationTypeId,
          documentNumber: v.documentNumber,
          issueDate: v.issueDate || null,
          expiryDate: v.expiryDate || null,
          isVerified: false,
          notes: v.notes || null,
        })
      }
      update={(id, cardId, v) =>
        employeeService.updateIdentificationCard(id, cardId, {
          id: cardId,
          identificationTypeId: v.identificationTypeId,
          documentNumber: v.documentNumber,
          issueDate: v.issueDate || null,
          expiryDate: v.expiryDate || null,
          notes: v.notes || null,
        })
      }
      remove={employeeService.removeIdentificationCard.bind(employeeService)}
      actions={[
        {
          label: (c) => (c.isVerified ? 'Mark unverified' : 'Mark verified'),
          run: (c) =>
            c.isVerified
              ? employeeService.unverifyIdentificationCard(employeeId, c.id)
              : employeeService.verifyIdentificationCard(employeeId, c.id, {
                  verifiedDate: new Date().toISOString(),
                }),
        },
      ]}
      columns={[
        { header: 'Type', cell: (c) => c.identificationTypeName || c.cardTypeName || '—' },
        // The list projection returns the number masked; the raw value is only on detail.
        { header: 'Number', cell: (c) => c.documentNumberMasked || c.cardNumber || '—' },
        { header: 'Issued', cell: (c) => c.issueDate?.slice(0, 10) || '—' },
        {
          header: 'Expires',
          cell: (c) =>
            c.expiryDate ? (
              <span className={isExpired(c.expiryDate) ? 'text-red-600' : undefined}>
                {c.expiryDate.slice(0, 10)}
              </span>
            ) : (
              '—'
            ),
        },
        { header: 'Issuing authority', cell: (c) => c.issuingAuthority || '—' },
        {
          header: 'Status',
          cell: (c) => (
            <div className="flex gap-1">
              {c.isVerified && <Badge variant="secondary">Verified</Badge>}
              {isExpired(c.expiryDate) && <Badge variant="outline">Expired</Badge>}
            </div>
          ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      // Editing needs the unmasked number, which only the detail endpoint returns.
      toForm={(c) => ({
        identificationTypeId: c.identificationTypeId,
        documentNumber: c.documentNumber ?? c.cardNumber ?? '',
        issueDate: c.issueDate?.slice(0, 10) ?? '',
        expiryDate: c.expiryDate?.slice(0, 10) ?? '',
        notes: c.notes ?? '',
      })}
      renderFields={(form) => (
        <>
          <SelectField
            form={form}
            name="identificationTypeId"
            label="Identification type"
            required
            options={typeOptions}
          />
          <TextField form={form} name="documentNumber" label="Document number" required />
          <FieldRow>
            <DateField form={form} name="issueDate" label="Issue date" />
            <DateField form={form} name="expiryDate" label="Expiry date" />
          </FieldRow>
          <TextareaField form={form} name="notes" label="Notes" />
        </>
      )}
    />
  );
}
