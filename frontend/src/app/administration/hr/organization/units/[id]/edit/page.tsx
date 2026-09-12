'use client';

import { useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRightLeft, Loader2, UserCog } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  OrganizationUnitForm,
  type OrganizationUnitFormValues,
} from '@/components/hr/organization/OrganizationUnitForm';
import { UnitChangeLog } from '@/components/hr/organization/UnitChangeLog';
import {
  ChangeUnitHeadDialog,
  MoveUnitDialog,
} from '@/components/hr/organization/UnitRestructureDialogs';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { organizationUnitHistoryService } from '@/services/hr/organization-unit-history.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';

/**
 * A single organisation unit: its details, and everything ever recorded against it.
 *
 * The change log is a tab here rather than a screen of its own because a change log without the
 * thing it logs is unreadable — and because this form is where the two changes it records are made,
 * so the record of the last restructure sits beside the control that performs the next one. The
 * cross-organisation view is the register at `/administration/hr/organization/unit-history`.
 */
export default function EditOrganizationUnitPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);
  const [moving, setMoving] = useState(false);
  const [changingHead, setChangingHead] = useState(false);

  const { data: unit, isLoading, isError } = useQuery({
    queryKey: ['hr', 'organization-units', id],
    queryFn: () => organizationUnitService.getById(id),
    enabled: !!id,
  });

  const { data: levels } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
  });

  const { data: units } = useQuery({
    queryKey: ['hr', 'organization-units', 'summary'],
    queryFn: () => organizationUnitService.getSummary(),
  });

  const { data: history, isLoading: historyLoading } = useQuery({
    queryKey: ['hr', 'organization-unit-history', 'unit', id],
    queryFn: () => organizationUnitHistoryService.getByUnit(id),
    enabled: !!id,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'organization-units'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'organization-unit-history'] });
  };

  // Exclude the unit itself from the parent options.
  const parentOptions = useMemo(
    () => (units ?? []).filter((u) => u.id !== id),
    [units, id],
  );

  const handleSubmit = async (values: OrganizationUnitFormValues) => {
    setSubmitting(true);
    try {
      await organizationUnitService.update(id, {
        id,
        name: values.name,
        code: values.code ?? '',
        accountCode: values.accountCode || null,
        description: values.description || null,
        organizationLevelId: values.organizationLevelId,
        parentUnitId: values.parentUnitId || null,
        headEmployeeId: values.headEmployeeId || null,
        sequence: values.sequence,
        isActive: values.isActive,
        // Only meaningful when the parent or the head moved — the server records nothing for a
        // rename, and ignores the reason accordingly.
        changeReason: values.changeReason || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'organization-units'] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'organization-unit-history'] });
      toast({ title: 'Success', description: 'Organization unit updated.' });
      router.push('/administration/hr/organization/units');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update organization unit.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="Edit Organization Unit"
        description={unit ? unit.name : 'Update this node of the organization hierarchy.'}
        backHref="/administration/hr/organization/units"
        actions={
          unit ? (
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => setChangingHead(true)}>
                <UserCog className="mr-2 h-4 w-4" />
                {unit.headEmployeeId ? 'Change head' : 'Appoint head'}
              </Button>
              <Button variant="outline" onClick={() => setMoving(true)}>
                <ArrowRightLeft className="mr-2 h-4 w-4" /> Move unit
              </Button>
            </div>
          ) : undefined
        }
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !unit ? (
        <EmptyState
          title="Unit not found"
          description="This organization unit may have been deleted."
        />
      ) : (
        <Tabs defaultValue="details">
          <TabsList>
            <TabsTrigger value="details">Details</TabsTrigger>
            <TabsTrigger value="history">
              Change log{history?.length ? ` (${history.length})` : ''}
            </TabsTrigger>
          </TabsList>

          <TabsContent value="details" className="mt-4">
            <OrganizationUnitForm
              levels={levels ?? []}
              units={parentOptions}
              isEdit
              initialHeadLabel={unit.headEmployeeName}
              defaultValues={{
                name: unit.name,
                code: unit.code ?? '',
                accountCode: unit.accountCode ?? '',
                description: unit.description ?? '',
                organizationLevelId: unit.organizationLevelId,
                parentUnitId: unit.parentUnitId ?? '',
                headEmployeeId: unit.headEmployeeId ?? '',
                sequence: unit.sequence,
                isActive: unit.isActive,
                changeReason: '',
              }}
              onSubmit={handleSubmit}
              submitting={submitting}
              submitLabel="Save Changes"
              onCancel={() => router.push('/administration/hr/organization/units')}
            />
          </TabsContent>

          <TabsContent value="history" className="mt-4">
            <Card>
              <CardHeader>
                <CardTitle>Change log</CardTitle>
                <CardDescription>
                  Where this unit has reported and who has led it, newest first. Entries are written
                  by the system and cannot be edited or removed.
                </CardDescription>
              </CardHeader>
              <CardContent>
                <UnitChangeLog
                  entries={history ?? []}
                  isLoading={historyLoading}
                  emptyTitle="Nothing recorded for this unit"
                  emptyDescription="Moving this unit under a different parent, or giving it a different head, writes an entry here."
                />
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      )}

      {unit && (
        <>
          <MoveUnitDialog
            unit={unit}
            open={moving}
            onOpenChange={setMoving}
            onDone={refresh}
          />
          <ChangeUnitHeadDialog
            unit={unit}
            open={changingHead}
            onOpenChange={setChangingHead}
            onDone={refresh}
          />
        </>
      )}
    </div>
  );
}
