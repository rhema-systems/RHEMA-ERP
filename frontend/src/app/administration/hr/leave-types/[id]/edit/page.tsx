'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  LeaveTypeForm,
  leaveTypeFormToRequest,
  type LeaveTypeFormValues,
} from '@/components/hr/leave/LeaveTypeForm';
import { leaveTypeService } from '@/services/hr/leave-type.service';

const str = (v: number | null | undefined) => (v === null || v === undefined ? '' : String(v));

export default function EditLeaveTypePage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  // The detail projection carries allowanceComponentIds, which the update must echo back
  // or the existing links are wiped.
  const { data: leaveType, isLoading, isError } = useQuery({
    queryKey: ['hr', 'leave-types', id, 'detail'],
    queryFn: () => leaveTypeService.getDetail(id),
    enabled: !!id,
  });

  const handleSubmit = async (values: LeaveTypeFormValues) => {
    setSubmitting(true);
    try {
      await leaveTypeService.update(id, {
        ...leaveTypeFormToRequest(values, leaveType?.allowanceComponentIds ?? []),
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-types'] });
      toast({ title: 'Success', description: 'Leave type updated.' });
      router.push(`/administration/hr/leave-types/${id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update leave type.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="Edit Leave Type"
        description={leaveType ? leaveType.name : 'Update this leave type.'}
        backHref={`/administration/hr/leave-types/${id}`}
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !leaveType ? (
        <EmptyState title="Leave type not found" description="It may have been removed." />
      ) : (
        <LeaveTypeForm
          defaultValues={{
            name: leaveType.name,
            code: leaveType.code,
            description: leaveType.description ?? '',
            isPaid: leaveType.isPaid,
            defaultDaysPerYear: leaveType.defaultDaysPerYear,
            maxDaysPerYear: leaveType.maxDaysPerYear,
            minDaysNotice: str(leaveType.minDaysNotice),
            requiresApproval: leaveType.requiresApproval,
            calendarColor: leaveType.calendarColor ?? '',
            hasSubTypes: leaveType.hasSubTypes,
            allowCarryOver: leaveType.allowCarryOver,
            maxCarryOverDays: str(leaveType.maxCarryOverDays),
            carryOverExpiryMonths: str(leaveType.carryOverExpiryMonths),
            forfeitUnusedAfterMonths: str(leaveType.forfeitUnusedAfterMonths),
            countWeekendsAsLeave: leaveType.countWeekendsAsLeave,
            countHolidaysAsLeave: leaveType.countHolidaysAsLeave,
            allowCashConversion: leaveType.allowCashConversion,
            requiresReliever: leaveType.requiresReliever,
            mandatoryAnnualLeave: leaveType.mandatoryAnnualLeave,
            requiresMedicalCertificate: leaveType.requiresMedicalCertificate,
            selfCertificationDays: String(leaveType.selfCertificationDays ?? 3),
            // Blank, not '0' — no board at all is a different statement from a zero threshold.
            medicalBoardThresholdDays:
              leaveType.medicalBoardThresholdDays == null
                ? ''
                : String(leaveType.medicalBoardThresholdDays),
            minServiceMonthsToAccess: str(leaveType.minServiceMonthsToAccess),
            encashmentRateBasis: leaveType.encashmentRateBasis,
            encashmentRatePerDay: str(leaveType.encashmentRatePerDay),
            encashmentWorkingDaysPerMonth: leaveType.encashmentWorkingDaysPerMonth,
            isActive: leaveType.isActive,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push(`/administration/hr/leave-types/${id}`)}
        />
      )}
    </div>
  );
}
