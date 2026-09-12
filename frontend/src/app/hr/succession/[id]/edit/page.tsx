'use client';

import { use } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { SuccessionPlanForm } from '@/components/hr/succession/SuccessionPlanForm';
import { successionService } from '@/services/hr/succession.service';

export default function EditSuccessionPlanPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);

  const { data: plan, isLoading } = useQuery({
    queryKey: ['succession-plans', id],
    queryFn: () => successionService.getById(id),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!plan) return null;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`Edit ${plan.planNumber}`}
        description={plan.planName}
        backHref={`/hr/succession/${id}`}
      />

      {plan.status === 'Approved' ? (
        // The service refuses this, so the screen says why rather than letting the user fill in a
        // form that cannot be saved.
        <Card>
          <CardContent className="p-6 text-sm text-muted-foreground">
            An approved plan is a record and cannot be edited. Create a new plan for the position
            instead — approving it will supersede this one and keep both in the version history.
          </CardContent>
        </Card>
      ) : (
        <SuccessionPlanForm plan={plan} />
      )}
    </div>
  );
}
