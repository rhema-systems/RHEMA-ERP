'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  SnapshotEditForm,
  toSnapshotUpdateRequest,
  type SnapshotFiguresFormValues,
} from '@/components/hr/safety/SnapshotForm';
import { safetyPerformanceService } from '@/services/hr/safety-performance.service';

/**
 * Correct the reported figures on an unreviewed snapshot. A reviewed one is locked — the server
 * refuses the save with 422 and its message is shown as-is.
 */
export default function EditPerformanceSnapshotPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const {
    data: snapshot,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['hr', 'safety-performance', 'detail', id],
    queryFn: () => safetyPerformanceService.getById(id),
    enabled: !!id,
  });

  const handleSubmit = async (values: SnapshotFiguresFormValues) => {
    setSubmitting(true);
    try {
      await safetyPerformanceService.update(id, toSnapshotUpdateRequest(id, values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-performance'] });
      toast({ title: 'Figures saved' });
      router.push(`/hr/safety/performance/${id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to save the figures.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (isError || !snapshot) {
    return (
      <div className="p-6">
        <EmptyState title="Snapshot not found" description="It may have been deleted." />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`Edit ${snapshot.snapshotNumber}`}
        description="Correct the reported figures. The period, location and preparer are fixed at creation."
        backHref={`/hr/safety/performance/${id}`}
      />
      <SnapshotEditForm
        snapshotNumber={snapshot.snapshotNumber}
        defaultValues={{
          totalAccidents: snapshot.totalAccidents,
          totalIncidents: snapshot.totalIncidents,
          totalNearMisses: snapshot.totalNearMisses,
          totalDangerousOccurrences: snapshot.totalDangerousOccurrences,
          totalFatalities: snapshot.totalFatalities,
          totalLostTimeInjuries: snapshot.totalLostTimeInjuries,
          lostTimeInjuryFrequencyRate: snapshot.lostTimeInjuryFrequencyRate ?? undefined,
          totalManHoursWorked: snapshot.totalManHoursWorked,
          totalLostDays: snapshot.totalLostDays,
          inspectionsPlanned: snapshot.inspectionsPlanned,
          inspectionsConducted: snapshot.inspectionsConducted,
          inspectionsOverdue: snapshot.inspectionsOverdue,
          correctiveActionsIssued: snapshot.correctiveActionsIssued,
          correctiveActionsCompleted: snapshot.correctiveActionsCompleted,
          correctiveActionsOverdue: snapshot.correctiveActionsOverdue,
          correctiveActionClosureRate: snapshot.correctiveActionClosureRate ?? undefined,
          trainingProgramsPlanned: snapshot.trainingProgramsPlanned,
          trainingProgramsConducted: snapshot.trainingProgramsConducted,
          totalTrainingHours: snapshot.totalTrainingHours,
          contractorsOnSite: snapshot.contractorsOnSite,
          contractorInspectionsConducted: snapshot.contractorInspectionsConducted,
          contractorNonComplianceNoticesIssued: snapshot.contractorNonComplianceNoticesIssued,
          contractorComplianceRate: snapshot.contractorComplianceRate ?? undefined,
          environmentalIncidents: snapshot.environmentalIncidents,
          environmentalIncidentsReportedToEpa: snapshot.environmentalIncidentsReportedToEpa,
          emergencyDrillsPlanned: snapshot.emergencyDrillsPlanned,
          emergencyDrillsConducted: snapshot.emergencyDrillsConducted,
          ppeComplianceRate: snapshot.ppeComplianceRate ?? undefined,
          housekeepingComplianceRating: snapshot.housekeepingComplianceRating ?? undefined,
          regulatoryObligationsTotal: snapshot.regulatoryObligationsTotal,
          regulatoryObligationsCompliant: snapshot.regulatoryObligationsCompliant,
          regulatoryObligationsNonCompliant: snapshot.regulatoryObligationsNonCompliant,
          regulatoryObligationsExpiringSoon: snapshot.regulatoryObligationsExpiringSoon,
          managementComments: snapshot.managementComments ?? '',
        }}
        onSubmit={handleSubmit}
        submitting={submitting}
        onCancel={() => router.push(`/hr/safety/performance/${id}`)}
      />
    </div>
  );
}
