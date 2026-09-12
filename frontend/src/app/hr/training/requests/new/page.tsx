'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { TextField, TextareaField, DateField, SelectField } from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { trainingRequestService } from '@/services/hr/training-request.service';
import { trainingProgramService } from '@/services/hr/training-program.service';

const schema = z.object({
  employeeId: z.string().min(1, 'Employee is required'),
  requestedTrainingTitle: z.string().min(1, 'Required').max(200),
  description: z.string().max(2000).optional().or(z.literal('')),
  justification: z.string().max(2000).optional().or(z.literal('')),
  requestDate: z.string().min(1, 'Required'),
  linkedProgramId: z.string().optional().or(z.literal('')),
});
type FormValues = z.infer<typeof schema>;

export default function NewTrainingRequestPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      employeeId: '',
      requestedTrainingTitle: '',
      description: '',
      justification: '',
      requestDate: new Date().toISOString().slice(0, 10),
      linkedProgramId: '',
    },
  });

  const { data: programs } = useQuery({
    queryKey: ['hr', 'training', 'programs', 'active'],
    queryFn: () => trainingProgramService.getActive(),
  });
  const programOptions = (programs ?? []).map((p) => ({
    value: p.id,
    label: `${p.programCode} — ${p.programName}`,
  }));

  const onSubmit = async (values: FormValues) => {
    setSubmitting(true);
    try {
      const created = await trainingRequestService.create({
        employeeId: values.employeeId,
        requestedTrainingTitle: values.requestedTrainingTitle,
        description: values.description || null,
        justification: values.justification || null,
        requestDate: new Date(values.requestDate).toISOString(),
        linkedProgramId: values.linkedProgramId || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'requests'] });
      toast({
        title: 'Created',
        description: `Request ${created.requestNumber} saved as a draft — submit it when it is ready.`,
      });
      router.push(`/hr/training/requests/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create the request.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Training Request"
        description="Ask for training that is not in the catalog yet. It is created as a draft."
        backHref="/hr/training/requests"
      />
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <Card>
          <CardHeader>
            <CardTitle>Request details</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <EmployeePickerField form={form} name="employeeId" label="Employee" required />
            <TextField
              form={form}
              name="requestedTrainingTitle"
              label="Training wanted"
              placeholder="Advanced Excel for Finance"
              required
            />
            <TextareaField form={form} name="description" label="Description" rows={3} />
            <TextareaField
              form={form}
              name="justification"
              label="Justification"
              rows={3}
              placeholder="Why is this needed?"
            />
            <DateField form={form} name="requestDate" label="Request date" required />
            <SelectField
              form={form}
              name="linkedProgramId"
              label="Existing programme (optional)"
              options={programOptions}
              allowEmpty
              emptyLabel="Not in the catalog"
            />
          </CardContent>
          <CardFooter className="flex justify-end gap-2">
            <Button
              variant="outline"
              type="button"
              onClick={() => router.push('/hr/training/requests')}
              disabled={submitting}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={submitting}>
              {submitting ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Save className="mr-2 h-4 w-4" />
              )}
              Create request
            </Button>
          </CardFooter>
        </Card>
      </form>
    </div>
  );
}
