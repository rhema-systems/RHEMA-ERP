'use client';

import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, Mail } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { hrCustomerKey } from '@/components/hr/common/FinanceCustomerPicker';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { consultantClientService } from '@/services/hr/consultant.service';
import { formatDate, formatDateTime, formatMoney } from '@/lib/hr/attendance-format';
import { BILLING_CYCLE_OPTIONS, ENGAGEMENT_STATUS_OPTIONS } from '@/types/hr/consultant';
import type {
  ClientEngagementSummary,
  ConsultantClientPortalAccountSummary,
  InvitePortalAccount,
} from '@/types/hr/consultant';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

const engagementSchema = z
  .object({
    engagementCode: z.string().min(1, 'A code is required').max(50),
    title: z.string().min(1, 'A title is required').max(200),
    description: z.string().max(2000).optional(),
    consultantId: z.string().min(1, 'Select a consultant'),
    startDate: z.string().min(1, 'Required'),
    endDate: z.string().optional(),
    hourlyRate: z.coerce.number().min(0.01),
    currency: z.string().length(3),
    billingCycle: z.enum(['Weekly', 'Fortnightly', 'Monthly', 'MilestoneBased', 'OnCompletion']),
    maxHoursPerWeek: z.coerce.number().min(0).max(168).optional(),
    contractValue: z.coerce.number().min(0).optional(),
    purchaseOrderNumber: z.string().max(100).optional(),
    status: z.enum(['Draft', 'Active', 'Suspended', 'Completed', 'Terminated']),
    notes: z.string().max(2000).optional(),
  })
  .refine((v) => !v.endDate || v.endDate >= v.startDate, {
    message: 'The end date cannot be before the start date',
    path: ['endDate'],
  });

type EngagementForm = z.input<typeof engagementSchema>;

const portalInviteSchema = z.object({
  email: z.string().email('Enter a valid email').max(200),
  contactName: z.string().max(200).optional(),
  contactRole: z.string().max(100).optional(),
});

type PortalInviteForm = z.input<typeof portalInviteSchema>;

/**
 * One client: its details, its engagements, and the portal accounts its contacts use to
 * confirm timesheets.
 */
export default function ConsultantClientDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data: client, isLoading, isError } = useQuery({
    queryKey: ['hr', 'consultant-clients', id],
    queryFn: () => consultantClientService.getById(id),
    enabled: !!id,
  });

  const { data: portalAccounts, isLoading: loadingAccounts } = useQuery({
    queryKey: ['hr', 'consultant-clients', id, 'portal-accounts'],
    queryFn: () => consultantClientService.getPortalAccounts(id),
    enabled: !!id,
  });

  // Lane 8, slice 6: the client stores only the Finance customer's ID, so the name is resolved
  // here. Shares the picker's cache key, so opening the edit dialog does not re-fetch it.
  const { data: financeCustomer } = useQuery({
    queryKey: hrCustomerKey(client?.financeCustomerId ?? ''),
    queryFn: () => consultantClientService.getCustomer(client?.financeCustomerId as string),
    enabled: !!client?.financeCustomerId,
  });

  const emptyEngagement: EngagementForm = {
    engagementCode: '',
    title: '',
    description: '',
    consultantId: '',
    startDate: '',
    endDate: '',
    hourlyRate: 0,
    currency: client?.currency ?? 'GHS',
    billingCycle: 'Monthly',
    maxHoursPerWeek: undefined,
    contractValue: undefined,
    purchaseOrderNumber: '',
    status: 'Active',
    notes: '',
  };

  // ResourceCollectionTab runs row actions through its own mutation, so it already blocks
  // a second click while this is in flight; no local busy flag is needed.
  const resendInvite = async (email: string) => {
    try {
      await consultantClientService.resendPortalInvite(id, email);
      await queryClient.invalidateQueries({
        queryKey: ['hr', 'consultant-clients', id, 'portal-accounts'],
      });
      toast({ title: 'Sent', description: `A fresh set-up link was emailed to ${email}.` });
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to resend the invite.',
        variant: 'destructive',
      });
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !client) {
    return (
      <div className="p-6">
        <EmptyState title="Client not found" description="It may have been removed." />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={client.clientName}
        description={`${client.clientCode}${client.industry ? ` · ${client.industry}` : ''}`}
        backHref="/hr/consulting/clients"
        actions={<StatusBadge active={client.isActive} />}
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="engagements">Engagements</TabsTrigger>
          <TabsTrigger value="portal">Portal accounts</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Contacts</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Primary contact" value={client.primaryContactName} />
              <InfoRow label="Email" value={client.primaryContactEmail} />
              <InfoRow label="Phone" value={client.primaryContactPhone} />
              <InfoRow label="Billing contact" value={client.billingContactName} />
              <InfoRow label="Billing email" value={client.billingContactEmail} />
              <InfoRow label="Billing phone" value={client.billingContactPhone} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Address &amp; billing</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Address" value={client.addressLine1} />
              <InfoRow label="City" value={client.city} />
              <InfoRow label="Region" value={client.region} />
              <InfoRow label="Country" value={client.countryName} />
              <InfoRow label="Currency" value={client.currency} />
              <InfoRow
                label="Payment terms"
                value={
                  client.defaultPaymentTermsDays ? `${client.defaultPaymentTermsDays} days` : '—'
                }
              />
              <InfoRow label="Tax ID" value={client.taxIdentificationNumber} />
              {/* Without a Finance customer nothing can be raised in Accounts Receivable — the
                  invoices this client's timesheets produce stay HR-side. */}
              <InfoRow
                label="Finance customer"
                value={
                  !client.financeCustomerId ? (
                    <span className="text-muted-foreground">Not linked to a Finance customer</span>
                  ) : financeCustomer ? (
                    <>
                      {financeCustomer.code} — {financeCustomer.name}
                      {financeCustomer.isActive ? '' : ' (inactive)'}
                    </>
                  ) : (
                    <span className="text-muted-foreground">Linked — loading…</span>
                  )
                }
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Activity</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Active engagements" value={client.activeEngagementCount} />
              <InfoRow label="Timesheets" value={client.timesheetCount} />
              <InfoRow label="Outstanding invoices" value={client.outstandingInvoiceCount} />
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="engagements" className="pt-4">
          <ResourceCollectionTab<ClientEngagementSummary, EngagementForm>
            parentId={id}
            title="engagements"
            singular="engagement"
            queryKey={['hr', 'consultant-clients', id, 'engagements']}
            invalidateKeys={[['hr', 'consultant-clients'], ['hr', 'client-engagements']]}
            dialogHint="An engagement sets the rate and billing cycle a consultant's timesheets are billed at."
            dialogClassName="sm:max-w-[640px]"
            emptyDescription="Add an engagement before consultants can log time against this client."
            list={(clientId) => consultantClientService.getEngagements(clientId)}
            create={(clientId, values) => {
              const v = engagementSchema.parse(values);
              return consultantClientService.addEngagement(clientId, {
                ...v,
                clientId,
                description: v.description || null,
                endDate: v.endDate || null,
                maxHoursPerWeek: v.maxHoursPerWeek ?? null,
                contractValue: v.contractValue ?? null,
                purchaseOrderNumber: v.purchaseOrderNumber || null,
                notes: v.notes || null,
              });
            }}
            // Editing runs through the engagements screen, which owns the lifecycle actions.
            allowUpdate={false}
            update={async () => undefined}
            getId={(e) => e.id}
            actions={[
              {
                label: 'Open engagement',
                run: async (e) => {
                  router.push(`/hr/consulting/engagements/${e.id}`);
                },
              },
            ]}
            columns={[
              { header: 'Code', cell: (e) => <span className="font-medium">{e.engagementCode}</span> },
              { header: 'Title', cell: (e) => e.title },
              { header: 'Consultant', cell: (e) => e.consultantName },
              { header: 'Starts', cell: (e) => formatDate(e.startDate) },
              { header: 'Ends', cell: (e) => formatDate(e.endDate) },
              {
                header: 'Rate',
                cell: (e) => formatMoney(e.hourlyRate, e.currency),
                className: 'text-right',
              },
              { header: 'Billing', cell: (e) => e.billingCycle },
              { header: 'Status', cell: (e) => <StatusBadge status={e.status} /> },
            ]}
            schema={engagementSchema as any}
            emptyForm={emptyEngagement}
            toForm={(e) => ({
              ...emptyEngagement,
              engagementCode: e.engagementCode,
              title: e.title,
              consultantId: e.consultantId,
              startDate: e.startDate,
              endDate: e.endDate ?? '',
              hourlyRate: e.hourlyRate,
              currency: e.currency,
              billingCycle: e.billingCycle,
              status: e.status,
            })}
            renderFields={(form) => (
              <>
                <FieldRow>
                  <TextField form={form} name="engagementCode" label="Engagement code" required />
                  <TextField form={form} name="title" label="Title" required />
                </FieldRow>
                <EmployeePickerField
                  form={form}
                  name="consultantId"
                  label="Consultant"
                  required
                />
                <TextareaField form={form} name="description" label="Description" rows={2} />
                <FieldRow>
                  <DateField form={form} name="startDate" label="Start date" required />
                  <DateField form={form} name="endDate" label="End date" />
                </FieldRow>
                <FieldRow>
                  <NumberField form={form} name="hourlyRate" label="Hourly rate" step="0.01" required />
                  <TextField form={form} name="currency" label="Currency" required />
                </FieldRow>
                <FieldRow>
                  <SelectField
                    form={form}
                    name="billingCycle"
                    label="Billing cycle"
                    required
                    options={BILLING_CYCLE_OPTIONS}
                  />
                  <SelectField
                    form={form}
                    name="status"
                    label="Status"
                    required
                    options={ENGAGEMENT_STATUS_OPTIONS}
                  />
                </FieldRow>
                <FieldRow>
                  <NumberField form={form} name="maxHoursPerWeek" label="Max hours / week" step="0.5" />
                  <NumberField form={form} name="contractValue" label="Contract value" step="0.01" />
                </FieldRow>
                <TextField form={form} name="purchaseOrderNumber" label="PO number" />
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />
        </TabsContent>

        <TabsContent value="portal" className="space-y-4 pt-4">
          <p className="text-sm text-muted-foreground">
            Portal accounts let a client contact confirm timesheets themselves. Invited contacts
            stay pending until they complete set-up from the emailed link.
          </p>

          <ResourceCollectionTab<ConsultantClientPortalAccountSummary, PortalInviteForm>
            parentId={id}
            title="portal accounts"
            singular="contact"
            queryKey={['hr', 'consultant-clients', id, 'portal-accounts']}
            dialogHint="Sends a set-up link to the address given. The contact chooses their own password."
            emptyDescription="Nobody at this client can sign in to confirm timesheets yet."
            list={(clientId) => consultantClientService.getPortalAccounts(clientId)}
            create={(clientId, values) =>
              consultantClientService.invitePortalAccount(clientId, {
                ...portalInviteSchema.parse(values),
                contactName: values.contactName || null,
                contactRole: values.contactRole || null,
              } as InvitePortalAccount)
            }
            // Accounts are self-managed by the contact; there is nothing to edit here.
            allowUpdate={false}
            update={async () => undefined}
            getId={(a) => a.id}
            actions={[
              {
                label: 'Resend invite',
                visible: (a) => a.isSetupPending,
                run: (a) => resendInvite(a.email),
              },
            ]}
            columns={[
              { header: 'Email', cell: (a) => <span className="font-medium">{a.email}</span> },
              { header: 'Name', cell: (a) => a.contactName || '—' },
              { header: 'Role', cell: (a) => a.contactRole || '—' },
              {
                header: 'State',
                cell: (a) =>
                  a.isSetupPending ? (
                    <Badge variant="secondary">Set-up pending</Badge>
                  ) : a.isEmailVerified ? (
                    <Badge>Verified</Badge>
                  ) : (
                    <Badge variant="outline">Unverified</Badge>
                  ),
              },
              { header: 'Last login', cell: (a) => formatDateTime(a.lastLoginAt) },
              { header: 'Active', cell: (a) => <StatusBadge active={a.isActive} /> },
            ]}
            schema={portalInviteSchema as any}
            emptyForm={{ email: '', contactName: '', contactRole: '' }}
            toForm={(a) => ({
              email: a.email,
              contactName: a.contactName ?? '',
              contactRole: a.contactRole ?? '',
            })}
            renderFields={(form) => (
              <>
                <TextField form={form} name="email" label="Email" type="email" required />
                <FieldRow>
                  <TextField form={form} name="contactName" label="Contact name" />
                  <TextField form={form} name="contactRole" label="Role" />
                </FieldRow>
              </>
            )}
          />

          {!loadingAccounts && (portalAccounts?.length ?? 0) > 0 && (
            <p className="text-xs text-muted-foreground">
              <Mail className="mr-1 inline h-3 w-3" />
              {portalAccounts?.filter((a) => a.isSetupPending).length ?? 0} invitation(s) still
              awaiting set-up.
            </p>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
