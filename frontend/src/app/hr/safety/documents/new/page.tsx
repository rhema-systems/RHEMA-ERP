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
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyDocumentsService } from '@/services/hr/safety-documents.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_DOCUMENT_CATEGORY_OPTIONS,
  type SheControlledDocumentCategory,
} from '@/types/hr/safety-documents';

/**
 * Registers a controlled SHE document. The number is server-assigned
 * (SHE-DOC-YYYY-NNNN) unless one is supplied; versions, approval and the
 * review cycle live on the detail page. New documents start as Draft.
 */
const documentSchema = z.object({
  documentNumber: z.string().max(30).optional().or(z.literal('')),
  title: z.string().min(1, 'A title is required').max(250),
  category: z.string().min(1, 'A category is required'),
  description: z.string().max(1000).optional().or(z.literal('')),
  keywords: z.string().max(500).optional().or(z.literal('')),
  ownerId: z.string().min(1, 'A document owner is required'),
  locationId: z.string().optional().or(z.literal('')),
  reviewFrequencyMonths: z.string().optional().or(z.literal('')),
  notes: z.string().max(1000).optional().or(z.literal('')),
});

type DocumentForm = z.input<typeof documentSchema>;

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function NewSheDocumentPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);

  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const form = useForm<DocumentForm>({
    resolver: zodResolver(documentSchema),
    defaultValues: {
      documentNumber: '',
      title: '',
      category: 'Policy',
      description: '',
      keywords: '',
      ownerId: '',
      locationId: '',
      reviewFrequencyMonths: '',
      notes: '',
    },
  });

  const submit = form.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = documentSchema.parse(values);
      const frequency = v.reviewFrequencyMonths ? Number(v.reviewFrequencyMonths) : null;
      const created = await safetyDocumentsService.create({
        documentNumber: blank(v.documentNumber),
        title: v.title,
        category: v.category as SheControlledDocumentCategory,
        description: blank(v.description),
        keywords: blank(v.keywords),
        ownerId: v.ownerId,
        locationId: blank(v.locationId),
        reviewFrequencyMonths: frequency && frequency > 0 ? frequency : null,
        notes: blank(v.notes),
      });
      toast({
        title: 'Document registered',
        description: `${created.documentNumber} starts as Draft — upload its first version next.`,
      });
      router.push(`/hr/safety/documents/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Registering the document failed.',
        variant: 'destructive',
      });
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Register SHE Document"
        description="Number is server-assigned unless supplied. Upload versions and approve from the detail page."
        backHref="/hr/safety/documents"
      />

      <form onSubmit={submit}>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Document details</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField form={form} name="title" label="Title" required />
              <SelectField
                form={form}
                name="category"
                label="Category"
                required
                options={SHE_DOCUMENT_CATEGORY_OPTIONS}
              />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <TextField form={form} name="documentNumber" label="Document number (blank = assigned)" />
              <TextField form={form} name="keywords" label="Keywords (for search)" />
            </FieldRow>
            <FieldRow>
              <EmployeePickerField form={form} name="ownerId" label="Document owner (custodian)" required />
              <SelectField
                form={form}
                name="locationId"
                label="Location (if site-specific)"
                options={locations.map((l) => ({ value: l.id, label: l.name }))}
              />
            </FieldRow>
            <FieldRow>
              <TextField
                form={form}
                name="reviewFrequencyMonths"
                label="Review frequency (months — sets the review date on approval)"
              />
              <TextField form={form} name="notes" label="Notes" />
            </FieldRow>
            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => router.back()} disabled={busy}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                Register document
              </Button>
            </div>
          </CardContent>
        </Card>
      </form>
    </div>
  );
}
