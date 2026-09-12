'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  SnapshotCreateForm,
  toSnapshotCreateRequest,
  type SnapshotCreateFormValues,
} from '@/components/hr/safety/SnapshotForm';
import { safetyPerformanceService } from '@/services/hr/safety-performance.service';
import { locationService } from '@/services/hr/location.service';

/**
 * Enter a period's reported KPI figures. The server refuses a duplicate snapshot number and a
 * second snapshot for the same period + location — both surface here with the rule's own message.
 */
export default function NewPerformanceSnapshotPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const handleSubmit = async (values: SnapshotCreateFormValues) => {
    setSubmitting(true);
    try {
      const created = await safetyPerformanceService.create(toSnapshotCreateRequest(values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-performance'] });
      toast({
        title: 'Snapshot created',
        description: `${created.snapshotNumber} — reported figures saved.`,
      });
      router.push(`/hr/safety/performance/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create the snapshot.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Performance Snapshot"
        description="The period's KPI figures as reported by the SHE officer — nothing here is computed. One snapshot per period and location."
        backHref="/hr/safety/performance"
      />
      <SnapshotCreateForm
        locations={locations}
        onSubmit={handleSubmit}
        submitting={submitting}
        onCancel={() => router.push('/hr/safety/performance')}
      />
    </div>
  );
}
