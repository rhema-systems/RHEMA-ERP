'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { z } from 'zod';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  TextField,
  TextareaField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyContractorService } from '@/services/hr/safety-contractor.service';

/**
 * Registers a contractor. The contractor code is a user-assigned code (e.g. CON-003) and must
 * be unique — a duplicate is refused. Every new contractor starts as Pending Assessment; the
 * pre-qualification outcome is recorded on the detail page, as are inductions, inspections,
 * notices and documents.
 */
const contractorSchema = z.object({
  contractorCode: z.string().min(1, 'A contractor code is required').max(30),
  companyName: z.string().min(1, 'A company name is required').max(200),
  tradingName: z.string().max(100).optional().or(z.literal('')),
  address: z.string().max(300).optional().or(z.literal('')),
  phone: z.string().max(50).optional().or(z.literal('')),
  email: z.string().max(100).optional().or(z.literal('')),
  registrationNumber: z.string().max(50).optional().or(z.literal('')),
  primaryContactName: z.string().max(100).optional().or(z.literal('')),
  primaryContactPhone: z.string().max(50).optional().or(z.literal('')),
  primaryContactEmail: z.string().max(100).optional().or(z.literal('')),
  isActive: z.boolean(),
});

type ContractorForm = z.input<typeof contractorSchema>;

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function NewContractorPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);

  const form = useForm<ContractorForm>({
    resolver: zodResolver(contractorSchema),
    defaultValues: {
      contractorCode: '',
      companyName: '',
      tradingName: '',
      address: '',
      phone: '',
      email: '',
      registrationNumber: '',
      primaryContactName: '',
      primaryContactPhone: '',
      primaryContactEmail: '',
      isActive: true,
    },
  });

  const submit = form.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = contractorSchema.parse(values);
      const created = await safetyContractorService.create({
        contractorCode: v.contractorCode,
        companyName: v.companyName,
        tradingName: blank(v.tradingName),
        address: blank(v.address),
        phone: blank(v.phone),
        email: blank(v.email),
        registrationNumber: blank(v.registrationNumber),
        primaryContactName: blank(v.primaryContactName),
        primaryContactPhone: blank(v.primaryContactPhone),
        primaryContactEmail: blank(v.primaryContactEmail),
        isActive: v.isActive,
      });
      toast({
        title: 'Contractor registered',
        description: `${created.contractorCode} starts as Pending Assessment — record the pre-qualification outcome on the detail page.`,
      });
      router.push(`/hr/safety/contractors/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Registering the contractor failed.',
        variant: 'destructive',
      });
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Register Contractor"
        description="A new contractor starts as Pending Assessment until pre-qualification is recorded."
        backHref="/hr/safety/contractors"
      />

      <form onSubmit={submit}>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Company</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField
                form={form}
                name="contractorCode"
                label="Contractor code (unique, e.g. CON-003)"
                required
              />
              <TextField form={form} name="companyName" label="Company name" required />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="tradingName" label="Trading name" />
              <TextField form={form} name="registrationNumber" label="Registration number" />
            </FieldRow>
            <TextareaField form={form} name="address" label="Address" rows={2} />
            <FieldRow>
              <TextField form={form} name="phone" label="Phone" />
              <TextField form={form} name="email" label="Email" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="primaryContactName" label="Primary contact" />
              <TextField form={form} name="primaryContactPhone" label="Contact phone" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="primaryContactEmail" label="Contact email" />
              <div />
            </FieldRow>
            <SwitchField form={form} name="isActive" label="Active" />
            <div className="flex justify-end gap-2">
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => router.push('/hr/safety/contractors')}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Register contractor
              </Button>
            </div>
          </CardContent>
        </Card>
      </form>
    </div>
  );
}
