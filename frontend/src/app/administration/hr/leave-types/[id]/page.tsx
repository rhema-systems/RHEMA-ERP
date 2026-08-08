'use client';

import { useParams, useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { LeaveSubTypesTab } from '@/components/hr/leave/LeaveSubTypesTab';
import { LeaveAllocationsTab } from '@/components/hr/leave/LeaveAllocationsTab';
import { LeaveEligibilityTab } from '@/components/hr/leave/LeaveEligibilityTab';
import { LeaveAccrualPoliciesTab } from '@/components/hr/leave/LeaveAccrualPoliciesTab';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { ENCASHMENT_RATE_BASIS_OPTIONS } from '@/types/hr/leave';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

function InfoCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">{title}</CardTitle>
      </CardHeader>
      <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">{children}</CardContent>
    </Card>
  );
}

const yn = (v: boolean) => (v ? 'Yes' : 'No');

export default function LeaveTypeDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';

  const { data: t, isLoading, isError } = useQuery({
    queryKey: ['hr', 'leave-types', id, 'detail'],
    queryFn: () => leaveTypeService.getDetail(id),
    enabled: !!id,
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !t) {
    return (
      <div className="p-6">
        <EmptyState title="Leave type not found" description="It may have been removed." />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={t.name}
        description={`${t.code}${t.description ? ` · ${t.description}` : ''}`}
        backHref="/administration/hr/leave-types"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge active={t.isActive} />
            <Button
              variant="outline"
              onClick={() => router.push(`/administration/hr/leave-types/${id}/edit`)}
            >
              <Pencil className="mr-2 h-4 w-4" /> Edit
            </Button>
          </div>
        }
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="sub-types">Sub-types</TabsTrigger>
          <TabsTrigger value="allocations">Allocations</TabsTrigger>
          <TabsTrigger value="eligibility">Eligibility</TabsTrigger>
          <TabsTrigger value="accrual">Accrual</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <InfoCard title="Entitlement">
            <InfoRow label="Default days / year" value={t.defaultDaysPerYear} />
            <InfoRow label="Max days / year" value={t.maxDaysPerYear} />
            <InfoRow label="Minimum notice (days)" value={t.minDaysNotice} />
            <InfoRow label="Min service to access (months)" value={t.minServiceMonthsToAccess} />
            <InfoRow label="Paid" value={yn(t.isPaid)} />
            <InfoRow label="Mandatory annual leave" value={yn(t.mandatoryAnnualLeave)} />
          </InfoCard>

          <InfoCard title="Counting & workflow">
            <InfoRow label="Counts weekends" value={yn(t.countWeekendsAsLeave)} />
            <InfoRow label="Counts holidays" value={yn(t.countHolidaysAsLeave)} />
            <InfoRow label="Requires approval" value={yn(t.requiresApproval)} />
            <InfoRow label="Requires reliever" value={yn(t.requiresReliever)} />
            <InfoRow label="Has sub-types" value={yn(t.hasSubTypes)} />
          </InfoCard>

          <InfoCard title="Carry-over & forfeiture">
            <InfoRow label="Allow carry-over" value={yn(t.allowCarryOver)} />
            <InfoRow label="Max carry-over days" value={t.maxCarryOverDays} />
            <InfoRow label="Carry-over expires (months)" value={t.carryOverExpiryMonths} />
            <InfoRow label="Forfeit unused after (months)" value={t.forfeitUnusedAfterMonths} />
          </InfoCard>

          <InfoCard title="Encashment">
            <InfoRow label="Allow cash conversion" value={yn(t.allowCashConversion)} />
            <InfoRow
              label="Rate basis"
              value={
                ENCASHMENT_RATE_BASIS_OPTIONS.find((o) => o.value === t.encashmentRateBasis)
                  ?.label ?? t.encashmentRateBasis
              }
            />
            <InfoRow label="Rate per day" value={t.encashmentRatePerDay} />
            <InfoRow label="Working days / month" value={t.encashmentWorkingDaysPerMonth} />
            <InfoRow
              label="Allowance components"
              value={
                t.allowanceComponentIds?.length ? (
                  <Badge variant="secondary">{t.allowanceComponentIds.length} linked</Badge>
                ) : undefined
              }
            />
          </InfoCard>
        </TabsContent>

        <TabsContent value="sub-types" className="pt-4">
          <LeaveSubTypesTab leaveTypeId={id} />
        </TabsContent>
        <TabsContent value="allocations" className="pt-4">
          <LeaveAllocationsTab leaveTypeId={id} />
        </TabsContent>
        <TabsContent value="eligibility" className="pt-4">
          <LeaveEligibilityTab leaveTypeId={id} />
        </TabsContent>
        <TabsContent value="accrual" className="pt-4">
          <LeaveAccrualPoliciesTab leaveTypeId={id} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
