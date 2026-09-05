'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { Loader2, Save, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import {
  NumberField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ASSESSMENT_SOURCE_OPTIONS, TRAINING_PRIORITY_OPTIONS } from '@/types/hr/training';
import { trainingNeedsAssessmentService } from '@/services/hr/training-needs-assessment.service';

const bulkSchema = z.object({
  year: z.coerce.number().min(2000).max(2100),
  source: z.enum(['PerformanceReview', 'SelfAssessment', 'ManagerRequest', 'SkillsGapAnalysis', 'JobRoleChange']),
  identifiedGaps: z.string().min(1, 'Required').max(4000),
  priority: z.enum(['Critical', 'High', 'Medium', 'Low']),
  additionalNotes: z.string().max(2000).optional().or(z.literal('')),
});
type BulkFormValues = z.infer<typeof bulkSchema>;

export default function BulkNeedsAssessmentPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [employees, setEmployees] = useState<{ id: string; label: string }[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const form = useForm<BulkFormValues>({
    resolver: zodResolver(bulkSchema) as any,
    defaultValues: {
      year: new Date().getFullYear(),
      source: 'SkillsGapAnalysis',
      identifiedGaps: '',
      priority: 'Medium',
      additionalNotes: '',
    },
  });

  const addEmployee = (id: string | null, label: string | null) => {
    if (!id || !label) return;
    if (employees.some((e) => e.id === id)) return;
    setEmployees((prev) => [...prev, { id, label }]);
  };

  const removeEmployee = (id: string) => setEmployees((prev) => prev.filter((e) => e.id !== id));

  const handleSubmit = async (values: BulkFormValues) => {
    if (employees.length === 0) {
      toast({ title: 'Error', description: 'Add at least one employee.', variant: 'destructive' });
      return;
    }
    setSubmitting(true);
    try {
      const result = await trainingNeedsAssessmentService.bulkCreate({
        employeeIds: employees.map((e) => e.id),
        year: values.year,
        source: values.source,
        identifiedGaps: values.identifiedGaps,
        priority: values.priority,
        additionalNotes: values.additionalNotes || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'needs-assessments'] });
      toast({
        title: 'Success',
        description: `Created ${result.createdCount} of ${result.requestedCount} requested assessments.`,
      });
      router.push('/administration/hr/training/needs-assessments');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to bulk-create assessments.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Bulk Training Needs Assessment"
        description="Record the same identified gap across several employees at once — for example, after a skills-gap analysis."
        backHref="/administration/hr/training/needs-assessments"
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Employees</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <EmployeePicker value={null} onChange={addEmployee} placeholder="Search and add employees…" />
          {employees.length === 0 ? (
            <EmptyState title="No employees added" description="Search above to add employees to this batch." />
          ) : (
            <div className="flex flex-wrap gap-2">
              {employees.map((e) => (
                <Badge key={e.id} variant="secondary" className="gap-1 py-1.5 pl-3 pr-1.5">
                  {e.label}
                  <button
                    type="button"
                    className="ml-1 rounded-full p-0.5 hover:bg-muted"
                    onClick={() => removeEmployee(e.id)}
                  >
                    <X className="h-3 w-3" />
                  </button>
                </Badge>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <form onSubmit={form.handleSubmit(handleSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Assessment details</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <NumberField form={form} name="year" label="Year" required />
              <SelectField form={form} name="source" label="Source" required options={ASSESSMENT_SOURCE_OPTIONS} />
            </FieldRow>
            <SelectField form={form} name="priority" label="Priority" required options={TRAINING_PRIORITY_OPTIONS} />
            <TextareaField form={form} name="identifiedGaps" label="Identified gaps" required rows={4} />
            <TextareaField form={form} name="additionalNotes" label="Additional notes" rows={3} />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push('/administration/hr/training/needs-assessments')}
            disabled={submitting}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {!submitting && <Save className="mr-2 h-4 w-4" />}
            Create {employees.length > 0 ? `for ${employees.length} employee${employees.length === 1 ? '' : 's'}` : ''}
          </Button>
        </div>
      </form>
    </div>
  );
}
