'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  TeamForm,
  emptyTeam,
  toCreateRequest,
  type TeamFormValues,
} from '@/components/hr/organization/TeamForm';
import { teamService } from '@/services/hr/team.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';

export default function NewTeamPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: units, isLoading: unitsLoading } = useQuery({
    queryKey: ['hr', 'organization-units', 'summary'],
    queryFn: () => organizationUnitService.getSummary(),
  });

  const { data: teams, isLoading: teamsLoading } = useQuery({
    queryKey: ['hr', 'teams', 'summary'],
    queryFn: () => teamService.getSummary(),
  });

  const handleSubmit = async (values: TeamFormValues) => {
    setSubmitting(true);
    try {
      const created = await teamService.create(toCreateRequest(values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'teams'] });
      toast({ title: 'Team created', description: `"${created.name}" is on the register.` });
      // Straight to the detail page: a team with no members is not yet useful, and the roster is
      // the next thing anyone creating a team wants.
      router.push(`/administration/hr/organization/teams/${created.id}`);
    } catch (error) {
      toast({
        title: 'Could not create the team',
        description: (error as Error)?.message || 'Failed to create the team.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-6 p-6">
      <PageHeader
        title="New Team"
        description="A working group — as distinct from the organization unit its people are posted into."
        backHref="/administration/hr/organization/teams"
      />

      {unitsLoading || teamsLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
        </div>
      ) : (
        <TeamForm
          units={units ?? []}
          parentCandidates={teams ?? []}
          defaultValues={emptyTeam}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Create Team"
          onCancel={() => router.push('/administration/hr/organization/teams')}
        />
      )}
    </div>
  );
}
