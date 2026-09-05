'use client';

/**
 * Area 25 slice 6 — request training, for yourself.
 *
 * Spec destination #14. The desk form at /hr/training/requests/new keeps its employee
 * picker (raising a request for someone else is the desk's act, per the controller); this
 * one sends the caller's own employee id from the auth context, which is also the only id
 * the server would accept from a plain employee. The programme list is the open reference
 * read every self-service form is allowed (W3 leave-type precedent).
 */

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
import { TextField, TextareaField, SelectField } from '@/components/hr/employee/tabs/fields';
import { useAuth } from '@/hooks/use-auth';
import { trainingRequestService } from '@/services/hr/training-request.service';
import { trainingProgramService } from '@/services/hr/training-program.service';

const schema = z.object({
  requestedTrainingTitle: z.string().min(1, 'Say what training you want').max(200),
  description: z.string().max(2000).optional().or(z.literal('')),
  justification: z.string().max(2000).optional().or(z.literal('')),
  linkedProgramId: z.string().optional().or(z.literal('')),
});
type FormValues = z.infer<typeof schema>;

export default function NewMyTrainingRequestPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();
  const employeeId = user?.employeeId ?? '';
  const [submitting, setSubmitting] = useState(false);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      requestedTrainingTitle: '',
      description: '',
      justification: '',
      linkedProgramId: '',
    },
  });

  const { data: programs } = useQuery({
    queryKey: ['me', 'training', 'programs', 'active'],
    queryFn: () => trainingProgramService.getActive(),
  });
  const programOptions = (programs ?? []).map((p) => ({
    value: p.id,
    label: `${p.programCode} — ${p.programName}`,
  }));

  const onSubmit = async (values: FormValues) => {
    if (!employeeId) return;
    setSubmitting(true);
    try {
      const created = await trainingRequestService.create({
        employeeId,
        requestedTrainingTitle: values.requestedTrainingTitle,
        description: values.description || null,
        justification: values.justification || null,
        // The request date is the server's "today" from the caller's point of view — the
        // desk form lets HR backdate; asking for training is always dated now.
        requestDate: new Date().toISOString(),
        linkedProgramId: values.linkedProgramId || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['me', 'training', 'requests'] });
      toast({
        title: 'Created',
        description: `Request ${created.requestNumber} saved as a draft — submit it when it is ready.`,
      });
      router.push(`/me/training/requests/${created.id}`);
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
    <div className="space-y-6">
      <PageHeader
        title="Request Training"
        description="Ask for training that is not on the calendar yet. It is created as a draft you submit."
        backHref="/me/training"
      />
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <Card>
          <CardHeader>
            <CardTitle>What do you want to learn?</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <TextField
              form={form}
              name="requestedTrainingTitle"
              label="Training wanted"
              placeholder="Advanced Excel for Finance"
              required
            />
            <TextareaField
              form={form}
              name="description"
              label="Description"
              rows={3}
              placeholder="What should it cover?"
            />
            <TextareaField
              form={form}
              name="justification"
              label="Justification"
              rows={3}
              placeholder="How does it help your work?"
            />
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
              onClick={() => router.push('/me/training')}
              disabled={submitting}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={submitting || !employeeId}>
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
