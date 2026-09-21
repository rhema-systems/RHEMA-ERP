'use client';

import { useRouter } from 'next/navigation';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { FinanceCustomerPicker } from '@/components/hr/common/FinanceCustomerPicker';
import { consultantClientService } from '@/services/hr/consultant.service';
import { countryService } from '@/services/hr/country.service';
import type { ConsultantClientSummary } from '@/types/hr/consultant';

/**
 * Client organisations that consultants are placed with.
 *
 * The client code is immutable once set — invoices, engagements and portal accounts all key
 * off it — so it is only offered on create.
 */
const clientSchema = z.object({
  clientName: z.string().min(1, 'A name is required').max(200),
  clientCode: z.string().min(1, 'A code is required').max(50),
  industry: z.string().max(200).optional(),
  description: z.string().max(1000).optional(),
  primaryContactName: z.string().max(200).optional(),
  primaryContactEmail: z.string().email('Enter a valid email').max(100).optional().or(z.literal('')),
  primaryContactPhone: z.string().max(50).optional(),
  addressLine1: z.string().max(500).optional(),
  // Carried, not edited here: the form has no inputs for these three yet, but a save used to send
  // null for them and wipe whatever the record held (the D-09/D-12 shape). They round-trip now.
  addressLine2: z.string().max(500).optional(),
  city: z.string().max(100).optional(),
  region: z.string().max(100).optional(),
  postalCode: z.string().max(20).optional(),
  countryId: z.string().optional(),
  billingContactName: z.string().max(200).optional(),
  billingContactEmail: z.string().email('Enter a valid email').max(100).optional().or(z.literal('')),
  billingContactPhone: z.string().max(50).optional(),
  taxIdentificationNumber: z.string().max(50).optional(),
  /** Optional: the Finance (Sales) customer the client is billed as — empty keeps invoices HR-side. */
  financeCustomerId: z.string().optional(),
  currency: z.string().length(3, 'Use a 3-letter currency code'),
  defaultPaymentTermsDays: z.coerce.number().min(0).max(365).optional(),
  isActive: z.boolean(),
  notes: z.string().max(2000).optional(),
});

type ClientForm = z.input<typeof clientSchema>;

const emptyClient: ClientForm = {
  clientName: '',
  clientCode: '',
  industry: '',
  description: '',
  primaryContactName: '',
  primaryContactEmail: '',
  primaryContactPhone: '',
  addressLine1: '',
  addressLine2: '',
  city: '',
  region: '',
  postalCode: '',
  countryId: '',
  billingContactName: '',
  billingContactEmail: '',
  billingContactPhone: '',
  taxIdentificationNumber: '',
  financeCustomerId: '',
  currency: 'GHS',
  defaultPaymentTermsDays: 30,
  isActive: true,
  notes: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function ConsultantClientsPage() {
  const router = useRouter();

  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'all'],
    queryFn: () => countryService.getAll(),
  });

  const countryOptions = (countries ?? []).map((c) => ({ value: c.id, label: c.name }));

  const toPayload = (values: ClientForm) => {
    const v = clientSchema.parse(values);
    return {
      clientName: v.clientName,
      industry: blank(v.industry),
      description: blank(v.description),
      primaryContactName: blank(v.primaryContactName),
      primaryContactEmail: blank(v.primaryContactEmail),
      primaryContactPhone: blank(v.primaryContactPhone),
      addressLine1: blank(v.addressLine1),
      addressLine2: blank(v.addressLine2),
      city: blank(v.city),
      region: blank(v.region),
      postalCode: blank(v.postalCode),
      countryId: blank(v.countryId),
      billingContactName: blank(v.billingContactName),
      billingContactEmail: blank(v.billingContactEmail),
      billingContactPhone: blank(v.billingContactPhone),
      taxIdentificationNumber: blank(v.taxIdentificationNumber),
      financeCustomerId: blank(v.financeCustomerId),
      currency: v.currency,
      defaultPaymentTermsDays: v.defaultPaymentTermsDays ?? null,
      isActive: v.isActive,
      notes: blank(v.notes),
    };
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Consultant Clients"
        description="Client organisations that consultants are placed with, and their billing details."
        backHref="/hr/consulting"
      />

      <ResourceListPanel<ConsultantClientSummary, ClientForm>
        title="clients"
        singular="client"
        queryKey={['hr', 'consultant-clients']}
        dialogHint="The client code is fixed after creation — engagements and invoices reference it."
        dialogClassName="sm:max-w-[680px]"
        list={() => consultantClientService.getAll()}
        create={(values) =>
          consultantClientService.create({
            ...toPayload(values),
            clientCode: clientSchema.parse(values).clientCode,
          })
        }
        update={(id, values) => consultantClientService.update(id, { id, ...toPayload(values) })}
        remove={(id) => consultantClientService.remove(id)}
        getId={(c) => c.id}
        actions={[
          {
            label: 'Open client',
            run: async (c) => {
              router.push(`/hr/consulting/clients/${c.id}`);
            },
          },
        ]}
        columns={[
          { header: 'Client', cell: (c) => <span className="font-medium">{c.clientName}</span> },
          { header: 'Code', cell: (c) => <span className="text-muted-foreground">{c.clientCode}</span> },
          { header: 'Contact', cell: (c) => c.primaryContactName || '—' },
          {
            header: 'Location',
            cell: (c) => [c.city, c.countryName].filter(Boolean).join(', ') || '—',
          },
          { header: 'Currency', cell: (c) => c.currency },
          {
            header: 'Engagements',
            cell: (c) => c.activeEngagementCount,
            className: 'text-right',
          },
          { header: 'Status', cell: (c) => <StatusBadge active={c.isActive} /> },
          {
            header: '',
            cell: (c) => (
              <Button
                variant="link"
                size="sm"
                className="h-auto p-0"
                onClick={(e) => {
                  e.stopPropagation();
                  router.push(`/hr/consulting/clients/${c.id}`);
                }}
              >
                Open
              </Button>
            ),
          },
        ]}
        schema={clientSchema as any}
        emptyForm={emptyClient}
        toForm={(c) => ({
          ...emptyClient,
          clientName: c.clientName,
          clientCode: c.clientCode,
          primaryContactName: c.primaryContactName ?? '',
          primaryContactEmail: c.primaryContactEmail ?? '',
          city: c.city ?? '',
          currency: c.currency,
          isActive: c.isActive,
        })}
        loadForEdit={async (c) => {
          // ⚠ The list returns SUMMARIES. Everything the summary does not carry — the industry,
          // the description, the address, the billing contacts, and now the Finance customer
          // link — would be blanked by a save made from the row alone (the D-09/D-12 shape), so
          // the full record is pulled once the dialog opens.
          const full = await consultantClientService.getById(c.id);
          return {
            ...emptyClient,
            clientName: full.clientName,
            clientCode: full.clientCode,
            industry: full.industry ?? '',
            description: full.description ?? '',
            primaryContactName: full.primaryContactName ?? '',
            primaryContactEmail: full.primaryContactEmail ?? '',
            primaryContactPhone: full.primaryContactPhone ?? '',
            addressLine1: full.addressLine1 ?? '',
            addressLine2: full.addressLine2 ?? '',
            city: full.city ?? '',
            region: full.region ?? '',
            postalCode: full.postalCode ?? '',
            countryId: full.countryId ?? '',
            billingContactName: full.billingContactName ?? '',
            billingContactEmail: full.billingContactEmail ?? '',
            billingContactPhone: full.billingContactPhone ?? '',
            taxIdentificationNumber: full.taxIdentificationNumber ?? '',
            financeCustomerId: full.financeCustomerId ?? '',
            currency: full.currency,
            defaultPaymentTermsDays: full.defaultPaymentTermsDays ?? undefined,
            isActive: full.isActive,
            notes: full.notes ?? '',
          };
        }}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="clientName" label="Client name" required />
              <TextField form={form} name="clientCode" label="Client code" required />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="industry" label="Industry" />
              <TextField form={form} name="currency" label="Currency" required placeholder="GHS" />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" rows={2} />

            <p className="pt-2 text-sm font-medium">Primary contact</p>
            <FieldRow>
              <TextField form={form} name="primaryContactName" label="Name" />
              <TextField form={form} name="primaryContactEmail" label="Email" type="email" />
            </FieldRow>
            <TextField form={form} name="primaryContactPhone" label="Phone" type="tel" />

            <p className="pt-2 text-sm font-medium">Address</p>
            <TextField form={form} name="addressLine1" label="Address" />
            <FieldRow>
              <TextField form={form} name="city" label="City" />
              <TextField form={form} name="region" label="Region" />
            </FieldRow>
            <SelectField
              form={form}
              name="countryId"
              label="Country"
              options={countryOptions}
              allowEmpty
            />

            <p className="pt-2 text-sm font-medium">Billing</p>
            <FieldRow>
              <TextField form={form} name="billingContactName" label="Billing contact" />
              <TextField form={form} name="billingContactEmail" label="Billing email" type="email" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="taxIdentificationNumber" label="Tax ID" />
              <NumberField
                form={form}
                name="defaultPaymentTermsDays"
                label="Payment terms (days)"
              />
            </FieldRow>
            {/* Lane 8, slice 6: linking the client to a Finance customer is what lets a timesheet
                invoice be raised in Accounts Receivable instead of living only in HR. */}
            <FinanceCustomerPicker
              id="financeCustomerId"
              value={(form.watch('financeCustomerId') as string | undefined) || null}
              onChange={(customerId) =>
                form.setValue('financeCustomerId', customerId ?? '', { shouldDirty: true })
              }
            />

            <SwitchField form={form} name="isActive" label="Active" />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </>
        )}
      />
    </div>
  );
}
